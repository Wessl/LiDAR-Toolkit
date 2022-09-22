using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class LiDAR : MonoBehaviour
{
    private Camera mainCam;

    public float coneAngle;
    public float fireRate;
    public float superScanMinTime = 1f;
    public int superScanSqrtNum = 200;
    public KeyCode lidarActivationKey = KeyCode.Mouse0;
    public KeyCode superScanKey = KeyCode.Y;
    public DrawPoints drawPointsRef;
    private float superScanWaitTime;
    public AudioClip superScanSFX;
    public AudioSource audioSource;

    private List<RaycastHit> hits = new List<RaycastHit>();
    // public PlayerController playerControllerRef;
    // public MouseLook mouseLookRef;
    private int activatorHitAmount = 0;
    public int enemyHitAmountTriggerThreshold = 30;
    private bool disabled;
    
    
    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main;
        superScanWaitTime = 1 / (superScanSqrtNum / superScanMinTime);
        disabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (disabled) return;
        if (Input.GetKeyDown(superScanKey))
        {
            StartCoroutine(SuperScan());
        }
        else if (Input.GetKey(lidarActivationKey))
        {
            DefaultScan();
        }
    }

    IEnumerator SuperScan()
    {
        // disable player and camera movement 
        // playerControllerRef.CanMove = false;
        // mouseLookRef.lookEnabled = false;
        
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
            Tuple<Vector3[],string[]> pointsHit = CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnPlane);
            drawPointsRef.UploadPointData(pointsHit.Item1, TagsToColors(pointsHit.Item2));     // It makes more sense to split these into two
            var timePassed = Time.time - timeBefore;
            yield return new WaitForSecondsRealtime(superScanWaitTime - timePassed);
        }
        // re-enable movement
        // playerControllerRef.CanMove = true;
        // mouseLookRef.lookEnabled = true;
    }

    void DefaultScan()
    {
        // Store variables relating to camera view dir and pos
        var facingDir = mainCam.gameObject.transform.forward;
        var cameraPos = mainCam.transform.position;
        var cameraRay = (cameraPos + facingDir);
        // Calculate perpendicular angles to view direction to generate circle on which points can be created
        var p = GetPerpendicular(facingDir);
        var q = Vector3.Cross(facingDir.normalized, p);
        int i_fireRate = (int)Mathf.Ceil(fireRate * Time.deltaTime);
        Vector3[] pointsOnDisc = new Vector3[i_fireRate];
        for (int i = 0; i < i_fireRate; i++)
        {
            pointsOnDisc[i] = GenRandPointDisc(p,q);
        }
        
        // DrawDebug(cameraRay, p, q, pointsOnDisc);

        Tuple<Vector3[],string[]> pointsHit = CheckRayIntersections(cameraPos, cameraRay-cameraPos, pointsOnDisc);
        drawPointsRef.UploadPointData(pointsHit.Item1, TagsToColors(pointsHit.Item2));     // It makes more sense to split these into two
    }

    private Vector3[] TagsToColors(string[] tags)
    {
        Vector3[] colors = new Vector3[tags.Length];
        int i = 0;
        foreach (var tag in tags)
        {
            switch (tag)
            {
                case "Untagged":
                    colors[i++] = new Vector3(1, 1, 0);
                    break;
                case "Interactible":
                    colors[i++] = new Vector3(0, 1, 0);
                    break;
                case "Goal":
                    colors[i++] = new Vector3(0.698f, 0.4f, 1f);
                    break;
                case "Enemy":
                    colors[i++] = new Vector3(0.733f, 0.031f, 0.031f);
                    activatorHitAmount++;
                    break;
                case "Wood":
                    colors[i++] = new Vector3(165/255f, 42/255f, 42/255f);
                    break;
                case "WashitsuWall":
                    colors[i++] = new Vector3(228 / 255f, 186 / 255f, 65 / 255f);
                    break;
                case "Tatami":
                    colors[i++] = new Vector3(68/255f, 48/255f, 24/255f);
                    break;
                case "Kanji":
                    colors[i++] = new Vector3(0f, 0f, 0);
                    activatorHitAmount++;
                    break;
                default:
                    colors[i++] = new Vector3(0.5f,0.4f,0.3f);
                    break;
            }
        }
        return colors;
    }


    private Tuple<Vector3[],String[]> CheckRayIntersections(Vector3 cameraPos, Vector3 cameraRay, Vector3[] points)
    {
        Vector3[] pointsHit = new Vector3[points.Length];
        string[] tagsOfPoints = new string[points.Length];
        int i = 0;
        foreach (var point in points)
        {
            RaycastHit hit;
            if (Physics.Raycast(cameraPos, (cameraRay + point), out hit))
            {
                tagsOfPoints[i] = hit.collider.tag;
                pointsHit[i++] = hit.point;
            }    
        }
        return new Tuple<Vector3[], string[]>(pointsHit, tagsOfPoints);
    }
    
    private Vector3 GenRandPointDisc(Vector3 p, Vector3 q)
    {
        // Generate random point in the PQ plane disc - actually makes sense if u think about it, a pretty simple alg
        var rmax = Mathf.Tan(Mathf.Deg2Rad * coneAngle);
        var theta = Random.Range(0f, 2 * Mathf.PI);
        var r = rmax * Mathf.Sqrt(Random.Range(0f, 1f));
        var v = r * (p * Mathf.Cos(theta) + q * Mathf.Sin(theta));
        return v;
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

    private Vector3 GetPerpendicular(Vector3 cameraRay)
    {
        /// https://stackoverflow.com/questions/39404576/cone-from-direction-vector
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
}
