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

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lidar = GetComponent<LiDAR>();
        scanType = lidar.scanType;
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
        } else if (scanType == LiDAR.ScanType.Puck)
        {
            lidar.VelodynePuckScan(sourcePositionTransform, 100);
        }

        lidar.ScanSizeAreaUpdate(Input.mouseScrollDelta);
    }
}
