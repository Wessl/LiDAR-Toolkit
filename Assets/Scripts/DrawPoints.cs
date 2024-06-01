using System;
using System.Globalization;
using System.Linq;
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
    [Header("Temporary hacky half-baked 'solutions'")]
    public bool printInfoToGUI = false;
    [Header("Shader File References")]
    public Shader pointShader;
    public Shader circleShader;
    public Shader squareShader;
    public Shader meshShaderURP;
    public Shader meshShaderBRP; 
    
    // Choose points (pixel size), circles (billboarded, world size), or meshes (3D, world size)
    public enum PointType
    {
        PixelPoint, CirclePoint, MeshPoint, SquarePoint
    }
    [FormerlySerializedAs("_pointType")]
    [Header("Point type options")]
    [SerializeField] 
    public PointType pointType;
    [FormerlySerializedAs("_pointMesh")] [SerializeField][Tooltip("E.g. sphere or cube mesh - don't use something with too many vertices")] 
    public Mesh pointMesh;
    [SerializeField][Range(0.0f, 1.0f)] [Tooltip("Size of spheres and meshes (Pixels are constant in size)")] 
    private float pointScale;
    [SerializeField]
    [Tooltip("Smooth out the edges of applicable points. Currently supports circle and square-type points.")]
    private bool smoothEdges;
    
    
    // Color overrides
    public bool overrideColor;
    public Color pointColor;
    public bool useColorGradient;
    public Color farPointColor;
    public float farPointDistance;
    [Tooltip("Works best with Circle and Mesh points.")]
    public bool fadePointsOverTime;
    public float fadeTime;
    public bool useNormalsForColor;
    
    // Private global variables
    private Material _material;                     // The material reference built from the active shader
    private int _bufIndex;                          // The index of points to create, always the latest one
    private bool _canStartRendering;
    private ComputeBuffer _posBuffer;
    private ComputeBuffer _colorBuffer;
    private ComputeBuffer _timeBuffer;              // Used for time-based effects
    private ComputeBuffer _normalBuffer;
    private ComputeBuffer[] _computeBuffers;
    private const int COMPUTEBUFFERCOUNT = 4;
    [Tooltip("Change at your own risk. Can cause crashes if you allocate more memory than what is available to you." +
             " Example: Three buffers are used to render points, so a limit of 576MB => 192MB per buffer, and 12 bytes" +
             " is needed to store each point (4 bytes per float, 3 per Vector3) => 16MB of points ~16.8 million points rendered." +
             "Also, note that just because you have a lot more VRAM than what you allocate here, rendering speed can still get quite low if you decide to use meshes instead of circles or pixels.")]
    public float hardVramLimitInMegabytes = 576f;
    private int _ComputeBufferSize = 16777216;       // 2^24. 3*4*16777216 = 192MB
    private int _strideVec3;
    private int _strideVec4;
    private Bounds bounds;
    private Camera mainCam;
    
    private const int DANGEROUS_VIDEO_MEMORY_AMOUNT = 1500000;
    private const double MEGABYTE = 1048576; 

    // Debug
    [SerializeField] private Text debugText;
    
    private static readonly int Camerapos = Shader.PropertyToID("camerapos");
    private static readonly int FadeTime = Shader.PropertyToID("fadeTime");
    private static readonly int Posbuffer = Shader.PropertyToID("posbuffer");
    private static readonly int Colorbuffer = Shader.PropertyToID("colorbuffer");
    private static readonly int Timebuffer = Shader.PropertyToID("timebuffer");
    private static readonly int Normalbuffer = Shader.PropertyToID("normalbuffer");

    private void Awake()
    {
        _posBuffer?.Release();
        _colorBuffer?.Release();
        _timeBuffer?.Release();
        _normalBuffer?.Release();
        _strideVec3 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3));
        _strideVec4 = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
        
        SetUpMaterials();
        _ComputeBufferSize = CalculateCompBufferSize();
        _posBuffer = new ComputeBuffer (_ComputeBufferSize, _strideVec3, ComputeBufferType.Default);
        _colorBuffer = new ComputeBuffer(_ComputeBufferSize, _strideVec4, ComputeBufferType.Default);
        _timeBuffer = new ComputeBuffer(_ComputeBufferSize, sizeof(float), ComputeBufferType.Default);
        _normalBuffer = new ComputeBuffer(_ComputeBufferSize, _strideVec3, ComputeBufferType.Default);
        _computeBuffers = new[] {_posBuffer, _colorBuffer, _timeBuffer, _normalBuffer};
        
        
        _bufIndex = 0;
        mainCam = Camera.main;
        _canStartRendering = false;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }
    
    

    private int CalculateCompBufferSize()
    {
        int compBufCount;
        // We're using N buffers in total. 12 bytes per index in each buffer. 1048576 is 1 megabyte.
        double singleBufferLength = (hardVramLimitInMegabytes/(COMPUTEBUFFERCOUNT*3*4)) * MEGABYTE;
        var exponent = (Math.Log(singleBufferLength, 2));
        // If the thing we got is not a perfect two exponent, make it so. 
        if (exponent % 1 == 0)
            compBufCount = (int)singleBufferLength; // The user picked a nice number :)
        else
        {
            var roundedDownExponent = Math.Floor(exponent);
            compBufCount = (int)Math.Pow(2, roundedDownExponent);     // the reason we are doing a calculation here is to "go back" to the input value of the user
            Debug.Log($"The hard limit in VRAM you chose was automatically changed to {compBufCount * 4 * 3 * COMPUTEBUFFERCOUNT / (MEGABYTE)}, this number will divide nicer into the number and size of buffers we're using.");
        }
        return compBufCount;
    }

    public void SetUpMaterials()
    {
        if (pointShader == null || circleShader == null || meshShaderBRP == null || meshShaderBRP == null || squareShader == null)
        {
            Debug.LogWarning("Can't set up materials when shader references are empty, please set them. ");
            return;
        }
        if (pointType == PointType.PixelPoint)
        {
            _material = new Material(pointShader);
        }

        else if (pointType == PointType.CirclePoint)
        {
            _material = new Material(circleShader);
            _material.SetFloat("_Scale", pointScale);
            _material.SetInt("_SmoothEdges", smoothEdges ? 1 : 0);
        }
        
        else if (pointType == PointType.MeshPoint)
        {
            _material = GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset ? new Material(meshShaderURP) : new Material(meshShaderBRP);
            Debug.Log("what is the renderpipeline? " + (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset));
            _material.enableInstancing = true;
            _material.SetFloat("_Scale", pointScale);
        } else if (pointType == PointType.SquarePoint)
        {
            _material = new Material(squareShader);
            _material.SetFloat("_Scale", pointScale);
            _material.SetInt("_SmoothEdges", smoothEdges ? 1 : 0);
        }
        if (!useColorGradient) farPointDistance = -1;
        _material.SetColor("farcolor", farPointColor);
        _material.SetFloat("fardist", farPointDistance);

        if (!fadePointsOverTime) fadeTime = 0;   // fadeTime is used to control whether points are faded in the shadersz
    }

    public void UploadPointData(Vector3[] pointPositions, Vector4[] colors, Vector3[] normals)
    {
        int amount = pointPositions.Length;
        int bufferStartIndex = _bufIndex % (_ComputeBufferSize);
        
        // We need to wrap around if surpass size - essentially implementing a circular buffer
        if (bufferStartIndex + amount > _ComputeBufferSize)
        {
            var firstChunkSize = _ComputeBufferSize - bufferStartIndex;
            var secondChunkSize = amount - firstChunkSize;
            if (fadePointsOverTime)
            {
                float[] timestamps = new float[amount];
                for (int i = 0; i < amount; i++) timestamps[i] = Time.time;
                _timeBuffer.SetData(timestamps, 0, bufferStartIndex, firstChunkSize);
                _timeBuffer.SetData(timestamps, firstChunkSize, 0, secondChunkSize);
            }
            _posBuffer.SetData(pointPositions, 0, bufferStartIndex, firstChunkSize);
            _posBuffer.SetData(pointPositions, firstChunkSize, 0, secondChunkSize);
            if (useNormalsForColor)
            {
                _normalBuffer.SetData(normals, 0, bufferStartIndex, firstChunkSize);
                _normalBuffer.SetData(normals, firstChunkSize, 0, secondChunkSize);
            }
            else
            {
                _colorBuffer.SetData(colors, 0, bufferStartIndex, firstChunkSize);
                _colorBuffer.SetData(colors, firstChunkSize, 0, secondChunkSize);
            }
        }
        else
        {
            _posBuffer.SetData(pointPositions, 0, bufferStartIndex, amount);
            if (fadePointsOverTime)
            {
                float[] timestamps = new float[amount];
                for (int i = 0; i < amount; i++) timestamps[i] = Time.time;
                _timeBuffer.SetData(timestamps, 0, bufferStartIndex, amount);
            }
            _posBuffer.SetData(pointPositions, 0, bufferStartIndex, amount);
            if (useNormalsForColor)
                _normalBuffer.SetData(normals, 0, bufferStartIndex, amount);
            else
                _colorBuffer.SetData(colors, 0, bufferStartIndex, amount);
        }   
        
        _bufIndex += amount;
        _canStartRendering = true;
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
        // Clearing easier
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllPoints();
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
        _material.SetVector(Camerapos, mainCam.transform.position);
        _material.SetFloat(FadeTime, fadeTime);
        _material.SetBuffer(Posbuffer, _posBuffer);
        _material.SetBuffer(Colorbuffer, _colorBuffer);
        _material.SetBuffer(Timebuffer, _timeBuffer);
        _material.SetBuffer(Normalbuffer, _normalBuffer);
        var count = Mathf.Min(_bufIndex, _ComputeBufferSize);
        if (pointType == PointType.PixelPoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Points, count, 1);
        }
        else if (pointType == PointType.CirclePoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, count);
        } else if (pointType == PointType.SquarePoint)
        {
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, count);
        }
        else if (pointType == PointType.MeshPoint)
        {
            Graphics.DrawMeshInstancedProcedural(pointMesh, 0, _material, bounds, count,  null, ShadowCastingMode.Off, false);
        }
    }

    public void OnValidate()
    {
        // Called whenever values in the inspector are changed
        _ComputeBufferSize = CalculateCompBufferSize();
        SetUpMaterials();
    }

    public void ClearAllPoints()
    {
        Awake();
    }

    void OnDestroy()
    {
        _computeBuffers.ToList().ForEach(buffer => buffer.Release());
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }
    
    void OnGUI()
    {
        if (!printInfoToGUI) return;
        float ratioOfTotalUsed = Mathf.Min(_bufIndex, _posBuffer.count) / (float)_posBuffer.count;    // 1.0 => 100% of allocatable memory used up.
        float videoMem = (_computeBuffers.ToList().Sum(buffer => buffer.count) * 3 * 4 / (float)MEGABYTE * ratioOfTotalUsed);
        long pointsRendered = _bufIndex > _ComputeBufferSize ? _ComputeBufferSize : _bufIndex;
        string text = $"{videoMem} MB of video memory used. {FormatNumber(pointsRendered)} points rendered.";
        if (videoMem > DANGEROUS_VIDEO_MEMORY_AMOUNT)
        {
            // todo: is the dangerous video memory amount really set correctly? 
            text = videoMem + " MB of video memory used - Warning! Don't go higher unless you know what you're doing.";
        }
        
        GUI.Label(new Rect(10, 10, 500, 40), text);
    }
    
    private static string FormatNumber(long num)
    {
        // Ensure number has max 3 significant digits (no rounding up can happen)
        long i = (long)Math.Pow(10, (int)Math.Max(0, Math.Log10(num) - 2));
        num = num / i * i;
        return num switch
        {
            >= 1000000000 => (num / 1000000000D).ToString("0.##") + "B",
            >= 1000000 => (num / 1000000D).ToString("0.##") + "M",
            >= 1000 => (num / 1000D).ToString("0.##") + "K",
            _ => num.ToString("#,0")
        };
    }
}
