using UnityEngine;
public class DrawPoints : MonoBehaviour
{
    // Does this really need to be an object? Ah well
    public Shader shader;
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
        
        _material = new Material(shader);
        _posBuffer = new ComputeBuffer (computeBufferCount, _stride, ComputeBufferType.Default);
        _colorBuffer = new ComputeBuffer(computeBufferCount, _stride, ComputeBufferType.Default);
        
        _bufIndex = 0;
        _canStartRendering = false;
    }

    public void SetUp(MeshTopology meshTopology)
    {
        _meshTopology = meshTopology;
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
        Graphics.DrawProceduralNow(MeshTopology.Points, _posBuffer.count, 1);
    }
    void OnDestroy()
    {
        _posBuffer.Release();   // we cant have dirty data laying around (this can crash your pc if you dont have it)
        _colorBuffer.Release();
    }
}
