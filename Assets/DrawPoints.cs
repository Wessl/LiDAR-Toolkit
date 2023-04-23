using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// This class handles actually drawing the points on the GPU, with data created by the LiDAR class.
/// Author Love Wessman github.com/Wessl/
/// </summary>
public class DrawPoints : MonoBehaviour
{
    [Header("Shader File References")]
    public Shader pointShader;
    public Shader circleShader;
    public Shader meshShaderURP;
    public Shader meshShaderBRP; 
    
    // Choose points (pixel size), circles (billboarded, world size), or meshes (3D, world size)
    public enum PointType
    {
        PixelPoint, CirclePoint, MeshPoint
    }
    [FormerlySerializedAs("_pointType")]
    [Header("Point type options")]
    [SerializeField] 
    public PointType pointType;
    [FormerlySerializedAs("_pointMesh")] [SerializeField][Tooltip("E.g. sphere or cube mesh - don't use something with too many vertices")] 
    public Mesh pointMesh;
    [SerializeField][Range(0.0f, 1.0f)] [Tooltip("Size of spheres and meshes (Pixels are constant in size)")] 
    private float pointScale;
    
    
    // Color overrides
    public bool overrideColor;
    public Color pointColor;
    public bool useColorGradient;
    public Color farPointColor;
    public float farPointDistance;
    [Tooltip("Works best with Circle and Mesh points.")]
    public bool fadePointsOverTime;
    public float fadeTime;
    
    // Private global variables
    private Material _material;                     // The material reference built from the active shader
    private int _bufIndex;                          // The index of points to create, always the latest one
    private bool _canStartRendering;
    private ComputeBuffer _posBuffer;
    private ComputeBuffer _colorBuffer;
    private ComputeBuffer _timeBuffer;              // Used for time-based effects
    [Tooltip("Change at your own risk. Can cause crashes if you allocate more memory than what is available to you." +
             " Example: Three buffers are used to render points, so a limit of 576MB => 192MB per buffer, and 12 bytes" +
             " is needed to store each point (4 bytes per float, 3 per Vector3) => 16MB of points ~16.8 million points rendered." +
             "Also, note that just because you have a lot more VRAM than what you allocate here, rendering speed can still get quite low if you decide to use meshes instead of circles or pixels.")]
    public float hardVramLimitInMegabytes = 576f;
    private int computeBufferCount = 16777216;       // 2^24. 3*4*16777216 = 192MB
    private int _strideVec3;
    private int _strideVec4;
    private Bounds bounds;
    private Camera mainCam;
    
    private const int DANGEROUS_VIDEO_MEMORY_AMOUNT = 1500000;

    // Debug
    [SerializeField] private Text debugText;

    private void Awake()
    {
        _posBuffer?.Release();
        _colorBuffer?.Release();
        _timeBuffer?.Release();
        _strideVec3 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        _strideVec4 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
        
        SetUpMaterials();
        CalculateCompBufferCount();
        _posBuffer = new ComputeBuffer (computeBufferCount, _strideVec3, ComputeBufferType.Default);
        _colorBuffer = new ComputeBuffer(computeBufferCount, _strideVec4, ComputeBufferType.Default);
        _timeBuffer = new ComputeBuffer(computeBufferCount, sizeof(float), ComputeBufferType.Default);

        _bufIndex = 0;
        mainCam = Camera.main;
        _canStartRendering = false;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }
    
    

    private void CalculateCompBufferCount()
    {
        // We're using three buffers in total. 12 bytes per index in each buffer. 1048576 is 1 megabyte.
        double singleBufferLength = (hardVramLimitInMegabytes/(3*3*4)) * 1048576;
        var exponent = (Math.Log(singleBufferLength, 2));
        // if the thing we got is not a perfect two exponent, make it so. 
        if (exponent % 1 == 0)
        {
            computeBufferCount = (int)singleBufferLength; // they picked a nice number :)
        }
        else
        {
            var roundedDownExponent = Math.Floor(exponent);
            computeBufferCount = (int)Math.Pow(2, roundedDownExponent);
            Debug.Log("The hard limit in VRAM you chose was automatically changed to " +
                      computeBufferCount * 4 * 3 * 3 / (1048576));
        }
        
    }

    public void SetUpMaterials()
    {
        if (pointType == PointType.PixelPoint)
        {
            _material = new Material(pointShader);
        }

        else if (pointType == PointType.CirclePoint)
        {
            _material = new Material(circleShader);
            _material.SetFloat("_Scale", pointScale);
        }
        
        else if (pointType == PointType.MeshPoint)
        {
            _material = GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset ? new Material(meshShaderURP) : new Material(meshShaderBRP);
            _material.enableInstancing = true;
            _material.SetFloat("_Scale", pointScale);
        }
        _material.SetColor("farcolor", farPointColor);
        _material.SetFloat("fardist", farPointDistance);

        if (!fadePointsOverTime) fadeTime = 0;   // fadeTime is used to control whether points are faded in the shadersz
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
        // Debug
        // debugText.text = "Points: " + _bufIndex;
    }
    
    
    void Update()
    {
        // DrawMeshInstancedProcedural needs to use Update() 
        if (_canStartRendering)
        {
            if (pointType == PointType.MeshPoint)
            {
                RenderPointsNow();
            }
        }
    }
    
    // For HDRP and URP
    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        RenderCirclesAndPoints();
    }

    // For BRP
    void OnRenderObject()
    {
        RenderCirclesAndPoints();
    }

    private void RenderCirclesAndPoints()
    {
        if (_canStartRendering)
        {
            if (pointType != PointType.MeshPoint)
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
        if (pointType == PointType.PixelPoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Points, count, 1);
        }
        else if (pointType == PointType.CirclePoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, count);
        } else if (pointType == PointType.MeshPoint)
        {
            Graphics.DrawMeshInstancedProcedural(pointMesh, 0, _material, bounds, count,  null, ShadowCastingMode.Off, false);
        }
    }

    public void OnValidate()
    {
        // Called whenever values in the inspector are changed
        SetUpMaterials();
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
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }
    
    void OnGUI()
    {
        float ratioOfTotalUsed = Mathf.Min(_bufIndex, _posBuffer.count) / (float)_posBuffer.count;    // 1.0 => 100% of allocatable memory used up. 
        float videoMem = (_posBuffer.count + _colorBuffer.count + _timeBuffer.count) * 3 * 4 / 1048576f * ratioOfTotalUsed;
        string text = videoMem + " MB of video memory used. " + FormatNumber(_bufIndex) + " points rendered.";
        if (videoMem > DANGEROUS_VIDEO_MEMORY_AMOUNT)
        {
            text = videoMem + " MB of video memory used - Warning! Don't go higher unless you know what you're doing.";
        } 
        
        GUI.Label(new Rect(10, 10, 500, 40), text);
    }
    
    private static string FormatNumber(long num)
    {
        // Ensure number has max 3 significant digits (no rounding up can happen)
        long i = (long)Math.Pow(10, (int)Math.Max(0, Math.Log10(num) - 2));
        num = num / i * i;

        if (num >= 1000000000)
            return (num / 1000000000D).ToString("0.##") + "B";
        if (num >= 1000000)
            return (num / 1000000D).ToString("0.##") + "M";
        if (num >= 1000)
            return (num / 1000D).ToString("0.##") + "K";

        return num.ToString("#,0");
    }
}
