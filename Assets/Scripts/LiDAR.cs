using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
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
    private float puckAngleCurrPlayerControl = 0;
    // for points hit - dont wanna re-allocate them every time we need em, so set them to a max value
    Vector3[] RaycastedPointsHit;
    Vector4[] RaycastedPointColors;
    Vector3[] RaycastedNormals;
    private NativeArray<RaycastHit> RaycastedResults;
    private NativeArray<RaycastCommand> RaycastedCommands;
    
    
    // General
    [Tooltip("Reference to the DrawPoints object. Necessary since it handles drawing points")]
    public DrawPoints drawPointsRef;
    [Tooltip("The LayerMask to use. Anything in the layers marked here will be hit, rest are ignored and passed through.")]
    public LayerMask layersToHit;
    public int lidarRange = 100;  // This hardly seems to make any difference in how fast physics.raycast functions. Interesting.

    [Header("Regular scan")]    
    public ScanType scanType;
    public ColorMode colorMode;
    public Color overrideColor;
    [Tooltip("The angle of the cone for the default scan")]
    [Range(0,60)]
    public float coneAngle;
    //private float puckAngleCurr = 0f;
    public float puckAngleIncrementer = 1f;
    [Tooltip("The length and width of the square scan")]
    [Range(0,1)]
    public float squareScanSize;
    [Tooltip("The amount of points to create per second")]
    public int fireRate;
    
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

    public enum ColorMode
    {
        HeightBased, RealUVColor, OverrideColor
    }
   

    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main;
        superScanWaitTime = 1 / (superScanSqrtNum / superScanMinTime);
        if (lineRenderer != null)
            lineRenderer.positionCount = 2;
        if (lineSpawnSource == null) lineSpawnSource = mainCam.transform;
    }

    public void ScanSizeAreaUpdate(Vector2 mouseScrollDelta)
    {
        Debug.Log("!uasdads");
        var scrollDelta = mouseScrollDelta.y;
        // Cone (for circle)
        coneAngle += scrollDelta;
        if (coneAngle > 60) coneAngle = 60;
        if (coneAngle < 0) coneAngle = 0;
        // Square
        squareScanSize += scrollDelta * 0.01f;  
        if (squareScanSize > 1) squareScanSize = 1;
        if (squareScanSize < 0) squareScanSize = 0;
    }

    public void DefaultScan(Vector3? facingDirOverride, Transform? sourceTransformOverride)
    {
        Profiler.BeginSample("DefaultScan");
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
            VelodynePuckScan(cameraPos, 100, ref puckAngleCurrPlayerControl);
        }
        Profiler.EndSample();
    }
    
    private void SquareScan(Transform cameraTransform, Vector3 facingDir)
    {
        // Calculate perpendicular angles to view direction to generate plane upon which points can be generated
        var p = cameraTransform.up;
        var q = cameraTransform.right;
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        using var pointsInSquare = new NativeArray<Vector3>(calculatedFireRate, Allocator.TempJob);
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
        DrawPoints(cameraTransform, facingDir, pointsInSquare.ToArray());
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
        DrawPoints(cameraTransform, facingDir, pointsOnLine);
    }

    private void DrawPoints(Transform cameraTransform, Vector3 facingDir, Vector3[] pointsOn2dPlane)
    {
        CheckRayIntersections(cameraTransform.position, facingDir, pointsOn2dPlane);
        drawPointsRef.UploadPointData(RaycastedPointsHit, RaycastedPointColors, RaycastedNormals);
        if (useLineRenderer && lineRenderer != null)
        {
            foreach (var hitPos in RaycastedPointsHit)
            {
                DrawRayBetweenPoints(facingDir, hitPos);
            }
        }
    }


    private void CircleScan(Transform cameraTransform, Vector3 facingDir)
    {
        Profiler.BeginSample("CircleScan");
        // Calculate perpendicular angles to view direction to generate circle on which points can be created
        var p = GetPerpendicular(facingDir);
        var q = Vector3.Cross(facingDir.normalized, p);
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        using var pointsOnDisc = new NativeArray<Vector3>(calculatedFireRate, Allocator.TempJob);
        var job = new BurstPointsOnDisc
        {
            FireRate = calculatedFireRate,
            P = p,
            Q = q,
            ConeAngle = coneAngle,
            seed = System.DateTime.Now.Ticks,
            Output = pointsOnDisc
        };
        job.Schedule().Complete();
        DrawPoints(cameraTransform, facingDir, pointsOnDisc.ToArray());
        Profiler.EndSample();
    }

    public void SphereScan(Transform sourceTransform, float len)
    {
        // Scan in a sphere around me :) 
        var dir = Random.onUnitSphere;
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        using var pointsInSphere = new NativeArray<Vector3>(calculatedFireRate, Allocator.TempJob);
        var job = new BurstPointsInRandomSphere
        {
            FireRate = calculatedFireRate,
            seed = (int)System.DateTime.Now.Ticks,
            len = len,
            Output = pointsInSphere
        };
        job.Schedule().Complete();
        DrawPoints(sourceTransform, Vector3.zero, pointsInSphere.ToArray());
    }
    
    // It's kind of like the sphere scan, except it creates continues lines around Y axis with some rotation increments.
    // Creates solid "lines" in circles on the ground around the user, continuously moving upwards
    public void VelodynePuckScan(Transform sourceTransform, float len, ref float puckAngleCurr)
    {
        int calculatedFireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        using var pointsInSphere = new NativeArray<Vector3>(calculatedFireRate, Allocator.TempJob);
        var angle = puckAngleCurr % 180;
        puckAngleCurr += puckAngleIncrementer;
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
        DrawPoints(sourceTransform, Vector3.zero, pointsInSphere.ToArray());
    }
    
    private static Vector3 GenRandPointDisc(Vector3 p, Vector3 q, float coneAngle, ref Unity.Mathematics.Random rng)
    {
        // Generate random point in the PQ plane disc
        var theta = rng.NextFloat(0, 2) * Mathf.PI;
        var r = (float)Math.Tan(Mathf.Deg2Rad * coneAngle) * Mathf.Sqrt(rng.NextFloat(0, 1));  // If you don't take the square root the points all end up in the middle. Uses about 0.25 ms/frame, probably worth it as a tradeoff. 
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
            CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnPlane);
            drawPointsRef.UploadPointData(RaycastedPointsHit, RaycastedPointColors, RaycastedNormals);  
            var timePassed = Time.time - timeBefore;
            yield return new WaitForSecondsRealtime(superScanWaitTime - timePassed);
        }
    }

    private void CheckRayIntersections(Vector3 cameraPos, Vector3 cameraRay, Vector3[] points)
    {
        Profiler.BeginSample("CheckRayIntersections()");
        // You probably don't need to remake these every frame. just create them with max possible point size, and put the changes into them, and then do not upload the things that do not get hit??? its easy. 
        Vector3[] tempRaycastedNormals = new Vector3[points.Length];
        Vector4[] tempRaycastedPointColors = new Vector4[points.Length];
        Vector3[] tempRaycastedPointsHit = new Vector3[points.Length];
        RaycastedResults = new NativeArray<RaycastHit>(points.Length, Allocator.TempJob);
        RaycastedCommands = new NativeArray<RaycastCommand>(points.Length, Allocator.TempJob);
        // Perform raycasts using RaycastCommand and wait for it to complete
        for (int i = 0; i < points.Length; i++)
        {
            RaycastedCommands[i] = new RaycastCommand(cameraPos, cameraRay + points[i], lidarRange, layersToHit);
        }
        
        // Schedule the batch of raycasts.
        JobHandle handle = RaycastCommand.ScheduleBatch(RaycastedCommands, RaycastedResults, 1, default(JobHandle));

        // Wait for the batch processing job to complete
        handle.Complete();
        int actualPointsHit = 0;
        // Copy the result. If batchedHit.collider is null there was no hit (by the way this is the slowest part of the whole thing)
        Profiler.BeginSample("For loop");
        for (var index = 0; index < RaycastedResults.Length; index++)
        {
            var hit = RaycastedResults[index];
            if (hit.distance > 0)
            {
                if (colorMode == ColorMode.OverrideColor) 
                    tempRaycastedPointColors[actualPointsHit] = overrideColor;
                else if (colorMode == ColorMode.HeightBased)
                    tempRaycastedPointColors[actualPointsHit] = TempColorScalerForLidar(hit.point.y, 5);
                else if (colorMode == ColorMode.RealUVColor)
                {
                    Profiler.BeginSample("GetColliderRelatedUVPointColor");
                    tempRaycastedPointColors[actualPointsHit] = GetColliderRelatedUVPointColor(hit);
                    Profiler.EndSample();
                }
                else
                {
                    Debug.LogError("Invalid color mode specified!");
                    break;
                }

                // fix this to do proper color management later
                tempRaycastedNormals[actualPointsHit] = hit.normal;
                tempRaycastedPointsHit[actualPointsHit] = hit.point;
                actualPointsHit++;
            }
        }
        Profiler.EndSample();
        RaycastedNormals = new Vector3[actualPointsHit];
        RaycastedPointsHit = new Vector3[actualPointsHit];
        RaycastedPointColors = new Vector4[actualPointsHit];
        Array.Copy(tempRaycastedNormals, RaycastedNormals, actualPointsHit);
        Array.Copy(tempRaycastedPointsHit, RaycastedPointsHit, actualPointsHit);
        Array.Copy(tempRaycastedPointColors, RaycastedPointColors, actualPointsHit);
        
        RaycastedResults.Dispose();
        RaycastedCommands.Dispose();
    }

    float scaleFloat(float inp, float max)
    {
        if (inp > max) return max;
        return inp / max;
    }

    Vector4 TempColorScalerForLidar(float inputHeight, float yMax)
    {
        float normalizedHeight = Mathf.Clamp01(inputHeight / yMax);
    
        if (normalizedHeight <= 0.5f)
        {
            // Blue to Yellow transition
            return Color.Lerp(Color.blue, Color.yellow, normalizedHeight * 2);
        }
        else
        {
            // Yellow to Red transition
            return Color.Lerp(Color.yellow, Color.red, (normalizedHeight - 0.5f) * 2);
        }
    }
    
    

    float WithinRange(float x, float a, float b)
    {
        return (Math.Sign((x - a) * (b - x)) + 1)/2f;
    }

    // todo: jobify this
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
    #region Burst
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
        [ReadOnly] public float ConeAngle;

        [WriteOnly]
        public NativeArray<Vector3> Output;
        
        public long seed;

        public void Execute()
        {
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)seed);
            for (int i = 0; i < FireRate; i++)
            {
                Output[i] = GenRandPointDisc(P,Q, ConeAngle, ref rng);
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
    #endregion
}
