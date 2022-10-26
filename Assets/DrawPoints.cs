using System;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawPoints : MonoBehaviour
{
    [Header("Shader File References")]
    public Shader pointShader;
    public Shader circleShader;
    public Shader sphereShader;
    
    // Choose points (pixel size), circles (billboarded, world size), or meshes (3D, world size)
    private enum PointType
    {
        PixelPoint, CirclePoint, MeshPoint
    }
    [Header("Point type options")]
    [SerializeField] 
    private PointType _pointType;
    [SerializeField][Tooltip("E.g. sphere or cube mesh - don't use something with too many vertices")] 
    private Mesh _pointMesh;
    [SerializeField][Range(0.0f, 1.0f)] [Tooltip("Size of spheres and meshes (Pixels are constant in size)")] 
    private float pointScale;
    
    
    // Color overrides
    public bool overrideColor;
    public Color pointColor;
    public bool useColorGradient;
    public Color farPointColor;
    public float farPointDistance;
    public bool fadePointsOverTime;
    public float fadeTime;
    
    // Private global variables
    private Material _material;                     // The material reference built from the active shader
    private int _bufIndex;                          // The index of points to create, always the latest one
    private bool _canStartRendering;
    private ComputeBuffer _posBuffer;
    private ComputeBuffer _colorBuffer;
    private ComputeBuffer _timeBuffer;              // Used for time-based effects
    private int computeBufferCount = 1048576;       // 2^20. 3*4*1048576 = 12MB
    private int _strideVec3;
    private int _strideVec4;
    private Bounds bounds;
    private Camera mainCam;

    private void Awake()
    {
        _posBuffer?.Release();
        _colorBuffer?.Release();
        _timeBuffer?.Release();
        _strideVec3 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        _strideVec4 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
        
        SetUp();
        _posBuffer = new ComputeBuffer (computeBufferCount, _strideVec3, ComputeBufferType.Default);
        _colorBuffer = new ComputeBuffer(computeBufferCount, _strideVec4, ComputeBufferType.Default);
        _timeBuffer = new ComputeBuffer(computeBufferCount, sizeof(float), ComputeBufferType.Default);

        _bufIndex = 0;
        mainCam = Camera.main;
        _canStartRendering = false;
    }

    public void SetUp()
    {
        if (_pointType == PointType.PixelPoint)
        {
            _material = new Material(pointShader);
        }

        else if (_pointType == PointType.CirclePoint)
        {
            _material = new Material(circleShader);
            _material.SetFloat("_Scale", pointScale);
        }
        
        else if (_pointType == PointType.MeshPoint)
        {
            _material = new Material(sphereShader);
            _material.enableInstancing = true;
            _material.SetFloat("_Scale", pointScale);
        }
        _material.SetColor("farcolor", farPointColor);
        _material.SetFloat("fardist", farPointDistance);
        
    }

    public void UploadPointData(Vector3[] pointPositions, Vector4[] colors, Vector3[] normals)
    {
        int amount = pointPositions.Length;
        int bufferStartIndex = _bufIndex % (computeBufferCount - amount);
        float[] timestamps = new float[amount];
        
        if (fadePointsOverTime)
        {
            for (int i = 0; i < amount; i++) timestamps[i] = Time.time;
            _timeBuffer.SetData(timestamps, 0, bufferStartIndex, amount);
        }
        
        _posBuffer.SetData (pointPositions, 0, bufferStartIndex, amount);
        _colorBuffer.SetData(colors, 0, bufferStartIndex, amount);
        
        _bufIndex += amount;
        _canStartRendering = true;
    }
    

    void Update()
    {
        // DrawMeshInstancedProcedural needs to use Update() 
        if (_canStartRendering)
        {
            if (_pointType == PointType.MeshPoint)
            {
                RenderPointsNow();
            }
        }
    }

    void OnRenderObject()
    {
        // Circles and Points use OnRenderObject
        if (_canStartRendering)
        {
            if (_pointType != PointType.MeshPoint)
            {
                RenderPointsNow();
            }
        }
    }


    public void RenderPointsNow()
    {
        bounds = new Bounds(Camera.main.transform.position, Vector3.one * 2f);
        _material.SetPass(0);
        _material.SetVector("camerapos", mainCam.transform.position);
        _material.SetFloat("fadeTime", fadeTime);
        _material.SetBuffer("posbuffer", _posBuffer);
        _material.SetBuffer("colorbuffer", _colorBuffer);
        _material.SetBuffer("timebuffer", _timeBuffer);
        var count = Mathf.Min(_bufIndex, computeBufferCount);
        if (_pointType == PointType.PixelPoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Points, count, 1);
        }
        else if (_pointType == PointType.CirclePoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, count);
        } else if (_pointType == PointType.MeshPoint)
        {
            Graphics.DrawMeshInstancedProcedural(_pointMesh, 0, _material, bounds, count,  null, ShadowCastingMode.Off, false);
        }
    }

    public void OnValidate()
    {
        // Called whenever values in the inspector are changed
        SetUp();
    }

    public void ClearAllPoints()
    {
        Awake();
    }

    void OnDestroy()
    {
        _posBuffer.Release();
        _colorBuffer.Release();
        _timeBuffer.Release();
    }
}
