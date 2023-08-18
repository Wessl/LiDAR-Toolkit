using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiDAR))]
public class PuckScanner : MonoBehaviour
{
    private LiDAR lidar;
    private LineRenderer lineRenderer;

    private void Start()
    {
        lidar = GetComponent<LiDAR>();
        lineRenderer = GetComponent<LineRenderer>();
        lidar.scanType = LiDAR.ScanType.Puck;
    }

    // Update is called once per frame
    void Update()
    {
        lineRenderer.positionCount = 0;    // Clear each frame
        Transform myTransform = this.transform;
        lidar.DefaultScan(myTransform.forward, myTransform);
    }
}
