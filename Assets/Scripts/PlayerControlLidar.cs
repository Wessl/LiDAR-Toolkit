using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(LiDAR))]
public class PlayerControlLidar : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private LiDAR lidar;
    public KeyCode lidarActivationKey = KeyCode.Mouse0;
    private LiDAR.ScanType scanType;
    [FormerlySerializedAs("sourcePosition")] [Tooltip("The source of the LiDAR rays being emitted. If you are shooting from the camera, use the camera, if a special object is emitting rays, use that object's position.")]
    public Transform sourcePositionTransform;

    private float puckAngleCurr;

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lidar = GetComponent<LiDAR>();
        scanType = lidar.scanType;
        puckAngleCurr = 0;
    }

    void LateUpdate()
    {
        lineRenderer.positionCount = 0;    // Clear each frame
        if (Input.GetKeyDown(lidar.superScanKey))
        {
            StartCoroutine(lidar.SuperScan());
        }
        else if (Input.GetKey(lidarActivationKey))
        {
            lidar.DefaultScan(null,null);
        } else if (scanType == LiDAR.ScanType.Sphere)
        {
            lidar.SphereScan(sourcePositionTransform, 100);
        } 

        if(Input.mouseScrollDelta.magnitude > 0)lidar.ScanSizeAreaUpdate(Input.mouseScrollDelta);
    }

    private void FixedUpdate()
    {
        if (scanType == LiDAR.ScanType.Puck)
        {
            float timeThisFrameMs = 0;
            while (timeThisFrameMs < 16.66 * 10e-3)
            {
                timeThisFrameMs += Time.deltaTime;
                lidar.VelodynePuckScan(sourcePositionTransform, 100, ref puckAngleCurr);
            }
        }
    }
}
