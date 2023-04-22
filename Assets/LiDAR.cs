using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * This class handles user input, firing raycasts into space, and decides where points should be drawn in space. 
 */

[RequireComponent(typeof(DrawPoints))]
[RequireComponent(typeof(LineRenderer))]
public class LiDAR : MonoBehaviour
{
    // Private variables, cached
    private List<RaycastHit> hits = new List<RaycastHit>();
    private Camera mainCam;
    private static readonly float discRMax = Mathf.Tan(Mathf.Deg2Rad * 30);
    private NativeArray<Unity.Mathematics.Random> _rngs;
    
    // General
    [Tooltip("Reference to the DrawPoints object. Necessary since it handles drawing points")]
    public DrawPoints drawPointsRef;
    [Tooltip("The LayerMask to use. Anything in the layers marked here will be hit, rest are ignored and passed through.")]
    public LayerMask layersToHit;
    public int lidarRange = 100;  // This hardly seems to make any difference in how fast physics.raycast functions. Interesting.
    
    [Header("Regular scan")]    
    public ScanType scanType;
    public KeyCode lidarActivationKey = KeyCode.Mouse0;
    [Tooltip("The angle of the cone for the default scan")]
    [Range(0,60)]
    public float coneAngle;
    [Tooltip("The length and width of the square scan")]
    [Range(0,1)]
    public float squareScanSize;
    [Tooltip("The amount of points to create per second")]
    public float fireRate;
    [Tooltip("Will artificially limit the amount of new points created per second if FPS gets below this limit.")]
    public int minimumAcceptableFPS = 20;
    
    [Header("Super scan")]
    public KeyCode superScanKey = KeyCode.Y;
    [Tooltip("The number of points in one dimension created by the super scan. E.g. 200, will create 200x200 in the super scan area")]
    public int superScanSqrtNum = 200;
    [Tooltip("The minimum amount of time to complete a super scan")]
    public float superScanMinTime = 1f;
    private float superScanWaitTime;
    
    [Header("Audio")] 
    public AudioClip defaultScanSFX;
    public AudioClip superScanSFX;
    public AudioSource audioSource;

    [Header("Line Renderer")]
    [Tooltip("Should lines be drawn between player and new point sources?")]
    public bool useLineRenderer;
    public LineRenderer lineRenderer;
    public Transform lineSpawnSource;
    public int maxLinesPerFrame;
    
    
    public enum ScanType
    {
        Circle, Line, Square
    }
    
    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main;
        superScanWaitTime = 1 / (superScanSqrtNum / superScanMinTime);
        lineRenderer.positionCount = 2;
        if (lineSpawnSource == null) lineSpawnSource = mainCam.transform;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        lineRenderer.positionCount = 0;    // Clear each frame
        if (Input.GetKeyDown(superScanKey))
        {
            StartCoroutine(SuperScan());
        }
        else if (Input.GetKey(lidarActivationKey))
        {
            DefaultScan();
        }

        ScanSizeAreaUpdate(Input.mouseScrollDelta);
        
    }

    private void ScanSizeAreaUpdate(Vector2 mouseScrollDelta)
    {
        var scrollDelta = mouseScrollDelta.y;
        // Cone (for circle)
        coneAngle += scrollDelta;
        if (coneAngle > 60) coneAngle = 60;
        if (coneAngle < 0) coneAngle = 0;
        // Square
        squareScanSize += scrollDelta * 0.01f;  
        if (squareScanSize > 1) squareScanSize = 1;
        if (squareScanSize < 0) squareScanSize = 0;
        OnValidate();
    }

    private void DefaultScan()
    {
        var facingDir = mainCam.gameObject.transform.forward;
        var cameraPos = mainCam.transform.position;
        var cameraRay = (cameraPos + facingDir);
        if (scanType == ScanType.Circle)
        {
            CircleScan(facingDir, cameraPos, cameraRay);
        } else if (scanType == ScanType.Line)
        {
            LineScan(facingDir, cameraPos, cameraRay);
        } else if (scanType == ScanType.Square)
        {
            SquareScan(facingDir, cameraPos, cameraRay);
        }
    }

    [BurstCompile]
    private void SquareScan(Vector3 facingDir, Vector3 cameraPos, Vector3 cameraRay)
    {
        // Calculate perpendicular angles to view direction to generate plane upon which points can be generated
        var p = mainCam.transform.up;
        var q = mainCam.transform.right;
        int i_fireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        Vector3[] pointsOnDisc = new Vector3[i_fireRate];
        for (int i = 0; i < i_fireRate; i++)
        {
            pointsOnDisc[i] = GenRandPointSquare(p,q,squareScanSize);
        }
        ValueTuple<Vector3[],Vector4[],Vector3[]> pointsHit = CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnDisc);
        drawPointsRef.UploadPointData(pointsHit.Item1, pointsHit.Item2, pointsHit.Item3);     // It makes more sense to split these into two
    }

    [BurstCompile]
    private void LineScan(Vector3 facingDir, Vector3 cameraPos, Vector3 cameraRay)
    {
        var right = mainCam.transform.right;
        
        int i_fireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        Vector3[] pointsOnLine = new Vector3[i_fireRate];
        for (int i = 0; i < i_fireRate; i++)
        {
            pointsOnLine[i] = right * Random.Range(-1f, 1f);
        }
        ValueTuple<Vector3[],Vector4[],Vector3[]> pointsHit = CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnLine);
        drawPointsRef.UploadPointData(pointsHit.Item1, pointsHit.Item2, pointsHit.Item3);
    }
    

    private void CircleScan(Vector3 facingDir, Vector3 cameraPos, Vector3 cameraRay)
    {
        // Calculate perpendicular angles to view direction to generate circle on which points can be created
        var p = GetPerpendicular(facingDir);
        var q = Vector3.Cross(facingDir.normalized, p);
        int i_fireRate = (int)Mathf.Ceil(fireRate * Mathf.Min(1f/minimumAcceptableFPS,Time.deltaTime));
        using var pointsOnDisc = new NativeArray<Vector3>(i_fireRate, Allocator.Persistent);
        var job = new BurstPointsOnDisc
        {
            FireRate = i_fireRate,
            P = p,
            Q = q,
            seed = (int)System.DateTime.Now.Ticks,
            Output = pointsOnDisc
        };
        job.Schedule().Complete();

        ValueTuple<Vector3[],Vector4[],Vector3[]> pointsHit = CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnDisc.ToArray());
        drawPointsRef.UploadPointData(pointsHit.Item1, pointsHit.Item2, pointsHit.Item3);     // It makes more sense to split these into two
    }
    
    [BurstCompile(CompileSynchronously = true)]
    private struct BurstPointsOnDisc : IJob
    {
        [ReadOnly] public int FireRate;
        [ReadOnly] public Vector3 P;
        [ReadOnly] public Vector3 Q;

        [WriteOnly]
        public NativeArray<Vector3> Output;
        
        public int seed;

        public void Execute()
        {
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
            for (int i = 0; i < FireRate; i++)
            {
                Output[i] = GenRandPointDisc(P,Q, ref rng);
            }
        }
    }
    // Now featuring bursty randomization
    private static Vector3 GenRandPointDisc(Vector3 p, Vector3 q, ref Unity.Mathematics.Random rng)
    {
        // Generate random point in the PQ plane disc
        var theta = rng.NextFloat(0, 2) * Mathf.PI;
        var r = discRMax * Mathf.Sqrt(rng.NextFloat(0, 1));  // If you don't take the square root the points all end up in the middle. Uses about 0.25 ms/frame, probably worth it as a tradeoff. 
        return r * (p * Mathf.Cos(theta) + q * Mathf.Sin(theta));
    }
    
    private IEnumerator SuperScan()
    {
        // lmao how the fuck did I figure all this out
        float aspect = mainCam.aspect;
        float magic = 1.75f;
        float fov = mainCam.fieldOfView;
        var mainCamGO = mainCam.gameObject;
        var facingDir = mainCamGO.transform.forward;
        var upDir =  mainCamGO.transform.up;
        var cameraPos = mainCam.transform.position;
        var cameraRay = (cameraPos + facingDir);
        // Activate Audio
        audioSource.PlayOneShot(superScanSFX);
        // Math
        var q = Vector3.Cross(facingDir.normalized, upDir.normalized);
        var r = Mathf.Tan(Mathf.Deg2Rad * (fov));
        Vector3[] pointsOnPlane = new Vector3[superScanSqrtNum];
        for (int i = 0; i < superScanSqrtNum; i++)
        {
            var timeBefore = Time.time;
            var meta = i / (float)superScanSqrtNum * Mathf.PI ;
            for (int j = 0; j < superScanSqrtNum; j++)
            {
                var theta = j / (float)superScanSqrtNum * Mathf.PI + Math.PI/2.0f;
                var v =  (Mathf.Cos(meta) * upDir/(magic) + Mathf.Sin((float)theta) * q/(magic/aspect));    // instead of magic numbers use randoms that are half of cell size and use screen ratio for other numbers
                pointsOnPlane[j] = v;
            }
            ValueTuple<Vector3[],Vector4[],Vector3[]> pointsHit = CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnPlane);
            drawPointsRef.UploadPointData(pointsHit.Item1, pointsHit.Item2, pointsHit.Item3);  
            var timePassed = Time.time - timeBefore;
            yield return new WaitForSecondsRealtime(superScanWaitTime - timePassed);
        }
    }

    

    private ValueTuple<Vector3[],Vector4[],Vector3[]> CheckRayIntersections(Vector3 cameraPos, Vector3 cameraRay, Vector3[] points)
    {
        Vector3[] pointsHit = new Vector3[points.Length];
        Vector4[] pointColors = new Vector4[points.Length];
        Vector3[] normals = new Vector3[points.Length];
        int i = 0;
        foreach (var point in points)
        {
            RaycastHit hit;
            if (Physics.Raycast(cameraPos, (cameraRay + point), out hit, lidarRange, layersToHit))
            {
                if (drawPointsRef.overrideColor)
                {
                    pointColors[i] = drawPointsRef.pointColor;
                }
                else
                {
                    pointColors[i] = GetColliderRelatedMeshRenderMaterialColor(hit);
                }

                // normals[i] = hit.normal;
                pointsHit[i++] = hit.point;
                
                if (useLineRenderer) DrawRayBetweenPoints(cameraRay, hit.point);
            }    
        }
        return new ValueTuple<Vector3[], Vector4[], Vector3[]>(pointsHit, pointColors, normals);
    }
    
   
    private Vector4 GetColliderRelatedMeshRenderMaterialColor(RaycastHit hit)
    {
        var baseMeshRenderer = hit.collider.gameObject.GetComponent<MeshRenderer>();
        if (baseMeshRenderer != null && baseMeshRenderer.material != null) return baseMeshRenderer.material.color;
        
        // It's possible either the parent, siblings, or descendants have color values. Expensive, but maybe necessary? Also we're returning as soon as something is found. 
        Transform parent = hit.transform.parent;
        var mesh = parent.GetComponent<MeshRenderer>();
        if (mesh) return mesh.material.color;
        // Check siblings! 
        var siblingMesh = parent.GetComponentsInChildren<MeshRenderer>();
        if (siblingMesh[0]) return siblingMesh[0].material.color;
        // Check children! 
        var childMesh = hit.transform.GetComponentsInChildren<MeshRenderer>();
        if (childMesh[0]) return childMesh[0].material.color;
        
        // Something strange must have happened, return a magenta color as default. 
        return Color.magenta;
    }
    
    
    private Vector3 GenRandPointSquare(Vector3 p, Vector3 q, float range)
    {
        float x = Random.Range(-range, range);
        float y = Random.Range(-range, range);
        Vector3 vec = p * x + q * y;
        return vec;
    }

    private void DrawRayBetweenPoints(Vector3 cameraRay, Vector3 endPoint)
    {
        var prevAmount = lineRenderer.positionCount;
        if (prevAmount > maxLinesPerFrame) return;
        var newPos = new Vector3[prevAmount + 2];
        lineRenderer.GetPositions(newPos);
        lineRenderer.positionCount += 2;
        newPos[prevAmount] = lineSpawnSource.position; 
        newPos[prevAmount+1] = endPoint;
        lineRenderer.SetPositions(newPos);
    }

    private Vector3 GetPerpendicular(Vector3 cameraRay)
    {
        /// https://stackoverflow.com/questions/39404576/cone-from-direction-vector
        ///  ask chatgpt if this can be optimized lmao
        cameraRay.Normalize();
        float max = float.NegativeInfinity;
        float min = float.PositiveInfinity;
        int axisMin = 1, axisMax = 1;
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(cameraRay[i]) > max)
            {
                axisMax = i;
                max = cameraRay[i];
            }
        }
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(cameraRay[i]) < min)
            {
                axisMin = i;
                min = cameraRay[i];
            }
        }
        // construct perpendicular
        Vector3 perp = new Vector3();
        var midIndex = 2 * (axisMax + axisMin) % 3;
        perp[axisMax] = cameraRay[midIndex];
        perp[midIndex] = -max;
        return perp.normalized;
    }

    private void OnValidate()
    {
        // discRMax = Mathf.Tan(Mathf.Deg2Rad * coneAngle);
    }

    private void DrawDebug(Vector3 cameraRay, Vector3 perpendicular, Vector3 q, Vector3[] pointOnDisc)
    {
        Debug.DrawLine(mainCam.transform.position, cameraRay, Color.green, 20);
        Debug.DrawLine(cameraRay, perpendicular + cameraRay, Color.red, 20);
        Debug.DrawLine(cameraRay, q + cameraRay, Color.yellow, 20);
        foreach (var point in pointOnDisc)
        {
            Debug.DrawLine(mainCam.transform.position, point + cameraRay, Color.yellow);
        }
    }
    
}
