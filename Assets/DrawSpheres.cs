using UnityEngine;
public class DrawSpheres : MonoBehaviour
{
    // Does this really need to be an object? Ah well
    public Shader pointShader;
    public Shader circleShader;
    // Choose points (pixel size) or circles (constant world size)
    private enum PointType
    {
        PixelPoint, CirclePoint
    }
    [SerializeField] private PointType _pointType;
    
    
    private Material _material;
    private int _bufIndex;
    private bool decreaseColorOverTime = false;
    private float colorDecreaseTimer = 0;

    private bool _canStartRendering;
    private ComputeBuffer _posBuffer;
    private ComputeBuffer _colorBuffer;
    private int computeBufferCount = 1048576; // 2^20. 3*4*1048576 = 12MB which is... nothing. still, buffers are seemingly routed through l2 cache which is smaller than 12MB, sometimes.. (actually idk, would love to find out)§
    private int _stride;
    
    // Mesh topology to render
    private MeshTopology _meshTopology = MeshTopology.Points;
    private void Awake()
    {
        _posBuffer?.Release();
        _colorBuffer?.Release();
        _stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        
        SetUp();
        _posBuffer = new ComputeBuffer (computeBufferCount, _stride, ComputeBufferType.Default);
        _colorBuffer = new ComputeBuffer(computeBufferCount, _stride, ComputeBufferType.Default);
        
        _bufIndex = 0;
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
        }
        float[] bufferX = new float[1023];
        float[] bufferY = new float[1023];
        for (int i=0; i<1023; i++)
        {
            bufferX[i] = Random.Range(0.0f, 120.0f);
            bufferY[i] = Random.Range(0.0f, 120.0f);
        }
        _material.SetFloatArray("BufferX", bufferX);
        _material.SetFloatArray("BufferY", bufferY);
    }

    public void UploadPointData(Vector3[] pointPositions, Vector3[] colors)
    {
        var amount = pointPositions.Length;
        _bufIndex += amount;
        _posBuffer.SetData (pointPositions, 0, _bufIndex % computeBufferCount, amount);
        _colorBuffer.SetData(colors, 0, _bufIndex % computeBufferCount, amount);
        // _material.SetBuffer ("posbuffer", _posBuffer);
        // _material.SetBuffer("colorbuffer", _colorBuffer);
        _canStartRendering = true;
    }
    

    void OnRenderObject()
    {
        RenderPointsNow();
        if (_canStartRendering)
        {
            RenderPointsNow();
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
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, 1023);

        }
    }
    void OnDestroy()
    {
        _posBuffer.Release();   // we cant have dirty data laying around (this can crash your pc if you dont have it)
        _colorBuffer.Release();
    }
}
