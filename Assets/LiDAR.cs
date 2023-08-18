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

/// <summary>
/// This class handles user input, firing raycasts into space, and decides where points should be drawn in space.
/// Simulates a handheld LiDAR scanner device. 
/// Author Love Wessman github.com/Wessl/
/// </summary>
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
    // TODO: DONT CAUSE REFERENCE ERRORS IF YOU DONT HAVE A LINE RENDERER ASSIGNED
    public bool useLineRenderer;
    public LineRenderer lineRenderer;
    public Transform lineSpawnSource;
    public int maxLinesPerFrame;
    
    public enum ScanType
    {
        Circle, Line, Square, Sphere, Puck
    }
    
    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main;
        superScanWaitTime = 1 / (superScanSqrtNum / superScanMinTime);
        lineRenderer.positionCount = 2;
        if (lineSpawnSource == null) lineSpawnSource = mainCam.transform;
    }

    public void ScanSizeAreaUpdate(Vector2 mouseScrollDelta)
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

    public void DefaultScan(Vector3? facingDirOverride, Transform? sourceTransformOverride)
    {
        var facingDir = facingDirOverride ?? mainCam.gameObject.transform.forward;
        var cameraPos = sourceTransformOverride ?? mainCam.transform;
        if (scanType == ScanType.Circle)
        {
            CircleScan(cameraPos, facingDir);
        } else if (scanType == ScanType.Line)
        {
            LineScan(cameraPos, facingDir);
        } else if (scanType == ScanType.Square)
        {
            SquareScan(cameraPos, facingDir);
        } else if (scanType == ScanType.Sphere)
        {
            SphereScan(cameraPos, 100);
        } else if (scanType == ScanType.Puck)
        {
            VelodynePuckScan(cameraPos, 100);
        }
    }
    
    private void SquareScan(Transform cameraTransform, Vector3 facingDir)
    {
        // Calculate perpendicular angles to view direction to generate plane upon which points can be generated
        var p = cameraTransform.up;
        var q = cameraTransform.right;
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        using var pointsInSquare = new NativeArray<Vector3>(calculatedFireRate, Allocator.Persistent);
        var job = new BurstPointsInSquare
        {
            FireRate = calculatedFireRate,
            P = p,
            Q = q,
            SquareScanSize = squareScanSize,
            seed = System.DateTime.Now.Ticks,
            Output = pointsInSquare
        };
        job.Schedule().Complete();

        CheckRayIntersections(cameraTransform.position, facingDir, pointsInSquare.ToArray(),
            out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals);
        drawPointsRef.UploadPointData(pointsHit, pointColors, normals);
    }

    public void LineScan(Transform cameraTransform, Vector3 facingDir)
    {
        var right = cameraTransform.transform.right;
        
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        Vector3[] pointsOnLine = new Vector3[calculatedFireRate];
        for (int i = 0; i < calculatedFireRate; i++)
        {
            pointsOnLine[i] = right * Random.Range(-1f, 1f);
        }
        CheckRayIntersections(cameraTransform.position, facingDir, pointsOnLine,
            out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals);
        drawPointsRef.UploadPointData(pointsHit, pointColors, normals);
    }
    

    private void CircleScan(Transform cameraTransform, Vector3 facingDir)
    {
        // Calculate perpendicular angles to view direction to generate circle on which points can be created
        var p = GetPerpendicular(facingDir);
        var q = Vector3.Cross(facingDir.normalized, p);
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Mathf.Min(1f/minimumAcceptableFPS,Time.deltaTime));
        using var pointsOnDisc = new NativeArray<Vector3>(calculatedFireRate, Allocator.Persistent);
        var job = new BurstPointsOnDisc
        {
            FireRate = calculatedFireRate,
            P = p,
            Q = q,
            seed = System.DateTime.Now.Ticks,
            Output = pointsOnDisc
        };
        job.Schedule().Complete();

        CheckRayIntersections(cameraTransform.position, facingDir, pointsOnDisc.ToArray(),
                out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals);
            drawPointsRef.UploadPointData(pointsHit, pointColors, normals);     // It makes more sense to split these into two
    }

    public void SphereScan(Transform sourceTransform, float len)
    {
        // Scan in a sphere around me :) 
        var dir = Random.onUnitSphere;
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Mathf.Min(1f/minimumAcceptableFPS,Time.deltaTime));
        using var pointsInSphere = new NativeArray<Vector3>(calculatedFireRate, Allocator.Persistent);
        var job = new BurstPointsInRandomSphere
        {
            FireRate = calculatedFireRate,
            seed = (int)System.DateTime.Now.Ticks,
            len = len,
            Output = pointsInSphere
        };
        job.Schedule().Complete();
        CheckRayIntersections(sourceTransform.position, Vector3.zero, pointsInSphere.ToArray(),
            out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals);
        drawPointsRef.UploadPointData(pointsHit, pointColors, normals);     
    }
    
    // It's kind of like the sphere scan, except it creates continues lines around Y axis with some rotation increments.
    // Creates solid "lines" in circles on the ground around the user, continuously moving upwards
    public void VelodynePuckScan(Transform sourceTransform, float len)
    {
        var dir = Random.onUnitSphere;
        // Maybe in this case the fire rate would have to be the point density in each circle around the axis?
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Mathf.Min(1f/minimumAcceptableFPS,Time.deltaTime));
        using var pointsInSphere = new NativeArray<Vector3>(calculatedFireRate, Allocator.Persistent);
        var angle = Random.Range(0, 180);
        var unitUpDir = new Vector3(0,1,0);
        var job = new BurstPointsInContinuousSphere
        {
            FireRate = calculatedFireRate,
            AngleAgainstUpDir = angle,
            UpDir = unitUpDir.normalized,
            len = len,
            Output = pointsInSphere
        };
        job.Schedule().Complete();
        CheckRayIntersections(sourceTransform.position, Vector3.zero, pointsInSphere.ToArray(),
            out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals);
        drawPointsRef.UploadPointData(pointsHit, pointColors, normals);     
    }
    
    
    // Now featuring bursty randomization
    private static Vector3 GenRandPointDisc(Vector3 p, Vector3 q, ref Unity.Mathematics.Random rng)
    {
        // Generate random point in the PQ plane disc
        var theta = rng.NextFloat(0, 2) * Mathf.PI;
        var r = discRMax * Mathf.Sqrt(rng.NextFloat(0, 1));  // If you don't take the square root the points all end up in the middle. Uses about 0.25 ms/frame, probably worth it as a tradeoff. 
        return r * (p * Mathf.Cos(theta) + q * Mathf.Sin(theta));
    }
    
    private static Vector3 GenRandPointSquare(Vector3 p, Vector3 q, float range, ref Unity.Mathematics.Random rng)
    {
        float x = rng.NextFloat(-range, range);
        float y = rng.NextFloat(-range, range);
        Vector3 vec = p * x + q * y;
        return vec;
    }
    
    public IEnumerator SuperScan()
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
            for (int j = 0; j < superScanSqrtNum; j++) // burstable? :eyes:
            {
                var theta = j / (float)superScanSqrtNum * Mathf.PI + Math.PI/2.0f;
                var v =  (Mathf.Cos(meta) * upDir/(magic) + Mathf.Sin((float)theta) * q/(magic/aspect));    // instead of magic numbers use randoms that are half of cell size and use screen ratio for other numbers
                pointsOnPlane[j] = v;
            }
            CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnPlane,
                out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals);
            drawPointsRef.UploadPointData(pointsHit, pointColors, normals);  
            var timePassed = Time.time - timeBefore;
            yield return new WaitForSecondsRealtime(superScanWaitTime - timePassed);
        }
    }

    
    // Todo... Maybe make this handle NativeArrays in order to not have to do .ToArray() from circlescan? Is ToArray even that expensive? 
    private void CheckRayIntersections(Vector3 cameraPos, Vector3 cameraRay, Vector3[] points, out Vector3[] pointsHit, out Vector4[] pointColors, out Vector3[] normals)
    {
        pointsHit = new Vector3[points.Length];
        pointColors = new Vector4[points.Length];
        normals = new Vector3[points.Length];

        int i = 0;
        RaycastHit[] hitBuffer = new RaycastHit[1];
        for (var index = 0; index < points.Length; index++)
        {
            Vector3 point = points[index];
            RaycastHit hit;
            if (Physics.Raycast(cameraPos, cameraRay + point, out hit, lidarRange, layersToHit))
            {
                if (drawPointsRef.overrideColor)
                {
                    pointColors[i] = drawPointsRef.pointColor;
                }
                else
                {
                    // pointColors[i] = GetColliderRelatedMeshRenderMaterialColor(hit);
                    pointColors[i] = GetColliderRelatedUVPointColor(hit);
                }

                normals[i] = hit.normal;
                pointsHit[i++] = hit.point;

                
            }
            if (useLineRenderer) DrawRayBetweenPoints(cameraPos, hit.point);
        }
    }

    private Vector4 GetColliderRelatedUVPointColor(RaycastHit hit)
    {
        // This is gonna be a super duper slow implementation because we are getting one pixel at a time like this. Should do it faster at some point. 
        var texture = (hit.collider.gameObject.GetComponent<MeshRenderer>().material.mainTexture as Texture2D);
        return texture ? texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y) : Color.magenta;
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
        // Idea from here https://stackoverflow.com/questions/39404576/cone-from-direction-vector
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
            if (Mathf.Abs(cameraRay[i]) < min)
            {
                axisMin = i;
                min = cameraRay[i];
            }
        }
        // construct perpendicular
        Vector3 perpendicular = new Vector3();
        var midIndex = 2 * (axisMax + axisMin) % 3;
        perpendicular[axisMax] = cameraRay[midIndex];
        perpendicular[midIndex] = -max;
        return perpendicular.normalized;
    }

    private void OnValidate()
    {
        // todo Make this work with burst
        // discRMax = Mathf.Tan(Mathf.Deg2Rad * coneAngle);
    }
    
    [BurstCompile(CompileSynchronously = true)]
    private struct BurstPointsInRandomSphere : IJob
    {
        [ReadOnly] public int FireRate;
        [ReadOnly] public float len;

        [WriteOnly]
        public NativeArray<Vector3> Output;
        
        public int seed;

        public void Execute()
        {
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
            for (int i = 0; i < FireRate; i++)
            {
                Vector3 vec3 = new Vector3(rng.NextFloat(-1, 1), rng.NextFloat(-1, 1), rng.NextFloat(-1, 1));
                Output[i] = vec3 * len;
            }
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    private struct BurstPointsInContinuousSphere : IJob
    {
        [ReadOnly] public int FireRate;
        [ReadOnly] public Vector3 UpDir;
        [ReadOnly] public float AngleAgainstUpDir;
        [ReadOnly] public float len;

        [WriteOnly]
        public NativeArray<Vector3> Output;
        
        public int seed;

        public void Execute()
        {
            //Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
            var newDir = Quaternion.Euler(0, 0, AngleAgainstUpDir) * UpDir;
            float rotIncrements = 360f / FireRate;
            float currRot = 0;
            for (int i = 0; i < FireRate; i++)
            {
                currRot += rotIncrements;
                Vector3 vec3 = (Quaternion.Euler(0, currRot, 0) * newDir);
                Output[i] = vec3 * len;
            }
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    private struct BurstPointsOnDisc : IJob
    {
        [ReadOnly] public int FireRate;
        [ReadOnly] public Vector3 P;
        [ReadOnly] public Vector3 Q;

        [WriteOnly]
        public NativeArray<Vector3> Output;
        
        public long seed;

        public void Execute()
        {
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
            for (int i = 0; i < FireRate; i++)
            {
                Output[i] = GenRandPointDisc(P,Q, ref rng);
            }
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    private struct BurstPointsInSquare : IJob
    {
        [ReadOnly] public int FireRate;
        [ReadOnly] public Vector3 P;
        [ReadOnly] public Vector3 Q;
        [ReadOnly] public float SquareScanSize;

        [WriteOnly]
        public NativeArray<Vector3> Output;
        
        public long seed;

        public void Execute()
        {
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
            for (int i = 0; i < FireRate; i++)
            {
                Output[i] = GenRandPointSquare(P,Q, SquareScanSize, ref rng);
            }
        }
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
