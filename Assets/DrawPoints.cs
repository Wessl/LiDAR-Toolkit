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
    [SerializeField] private PointType _pointType;
    [SerializeField][Tooltip("E.g. sphere or cube mesh")] private Mesh _pointMesh;
    [SerializeField][Range(0.0f, 1.0f)] [Tooltip("Only applies to spheres atm")] private float pointScale;
    
    // Color overrides
    public bool overrideColor;
    public Color pointColor;
    
    private Material _material;
    private int _bufIndex;
    private bool decreaseColorOverTime = false;
    private float colorDecreaseTimer = 0;

    private bool _canStartRendering;
    private ComputeBuffer _posBuffer;
    private ComputeBuffer _colorBuffer;
    private int computeBufferCount = 1048576; // 2^20. 3*4*1048576 = 12MB which is... nothing. still, buffers are seemingly routed through l2 cache which is smaller than 12MB, sometimes.. (actually idk, would love to find out)§
    private int _strideVec3;
    private int _strideVec4;
    private Bounds bounds;
    
    
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
        bounds = new Bounds(Camera.main.transform.position, Vector3.one * (1000f)); // this should probably be done better imho
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
        
        
    }

    public void UploadPointData(Vector3[] pointPositions, Vector4[] colors, Vector3[] normals)
    {
        var amount = pointPositions.Length;
        _bufIndex += amount;
        _posBuffer.SetData (pointPositions, 0, _bufIndex % computeBufferCount, amount);
        _colorBuffer.SetData(colors, 0, _bufIndex % computeBufferCount, amount);
        // _material.SetBuffer ("posbuffer", _posBuffer);
        // _material.SetBuffer("colorbuffer", _colorBuffer);
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
        _material.SetPass(0);
        _material.SetBuffer ("posbuffer", _posBuffer);
        _material.SetBuffer("colorbuffer", _colorBuffer);
        if (_pointType == PointType.PixelPoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Points, _posBuffer.count, 1);
        }
        else if (_pointType == PointType.CirclePoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, _bufIndex);
        } else if (_pointType == PointType.MeshPoint)
        {
            Graphics.DrawMeshInstancedProcedural(_pointMesh, 0, _material, bounds, _bufIndex,  null, ShadowCastingMode.Off, false);
        }
    }

    private void OnValidate()
    {
        // Called whenever values in the inspector are changed
        SetUp();
    }

    void OnDestroy()
    {
        _posBuffer.Release();
        _colorBuffer.Release();
    }
}
