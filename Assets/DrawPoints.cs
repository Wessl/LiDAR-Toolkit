using System;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawPoints : MonoBehaviour
{
    // Does this really need to be an object? Ah well
    public Shader pointShader;
    public Shader circleShader;
    public Shader sphereShader;
    // Choose points (pixel size) or circles (constant world size)
    private enum PointType
    {
        PixelPoint, CirclePoint, MeshPoint
    }
    [SerializeField] 
    private PointType _pointType;
    [SerializeField][Tooltip("E.g. sphere or cube mesh")] 
    private Mesh _pointMesh;
    [SerializeField][Range(0.0f, 1.0f)] [Tooltip("Only applies to spheres atm")] 
    private float pointScale;

    
    // Color overrides
    public bool overrideColor;
    public Color pointColor;
    public bool useColorGradient;
    public Color farPointColor;
    public float farPointDistance;
    
    private Material _material;
    private int _bufIndex;
    private bool decreaseColorOverTime = false;
    private float colorDecreaseTimer = 0;

    private bool _canStartRendering;
    private ComputeBuffer _posBuffer;
    private ComputeBuffer _colorBuffer;
    private int computeBufferCount = 104857; // 2^20. 3*4*1048576 = 12MB which is... nothing. still, buffers are seemingly routed through l2 cache which is smaller than 12MB, sometimes.. (actually idk, would love to find out)§
    private int _strideVec3;
    private int _strideVec4;
    private Bounds bounds;
    private Camera mainCam;


    // Mesh topology to render
    private MeshTopology _meshTopology = MeshTopology.Points;
    private void Awake()
    {
        _posBuffer?.Release();
        _colorBuffer?.Release();
        _strideVec3 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        _strideVec4 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
        
        SetUp();
        _posBuffer = new ComputeBuffer (computeBufferCount, _strideVec3, ComputeBufferType.Default);
        _colorBuffer = new ComputeBuffer(computeBufferCount, _strideVec4, ComputeBufferType.Default);

        _bufIndex = 0;
        mainCam = Camera.main;
        _canStartRendering = false;
    }

    public void SetUp()
    {
        
        if (_pointType == PointType.PixelPoint)
        {
            _material = new Material(pointShader);
            _meshTopology = MeshTopology.Points;
        }

        else if (_pointType == PointType.CirclePoint)
        {
            _material = new Material(circleShader);
            _meshTopology = MeshTopology.Triangles;
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
        var amount = pointPositions.Length;
        _posBuffer.SetData (pointPositions, 0, _bufIndex % (computeBufferCount-amount), amount);
        _colorBuffer.SetData(colors, 0, _bufIndex % (computeBufferCount-amount), amount);
        _bufIndex += amount;
        _canStartRendering = true;
    }
    

    void Update()
    {
        // 3D Spheres need to use Update() 
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
        _material.SetBuffer("posbuffer", _posBuffer);
        _material.SetBuffer("colorbuffer", _colorBuffer);
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
    }
}
