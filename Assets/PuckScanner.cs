using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiDAR))]
public class PuckScanner : MonoBehaviour
{
    private LiDAR lidar;
    private LineRenderer lineRenderer;
    [Range(0,180)]
    public float angleMax = 180;
    public float angleIncrement = 1;
    public int circlesPerFrame = 10;

    private void Start()
    {
        lidar = GetComponent<LiDAR>();
        lineRenderer = GetComponent<LineRenderer>();
        lidar.scanType = LiDAR.ScanType.Puck;
        lidar.puckAngleIncrementer = angleIncrement;
    }

    // Maybe instead of FixedUpdate find some other way of doing this...
    void FixedUpdate()
    {
        lineRenderer.positionCount = 0;    // Clear each frame
        Transform myTransform = this.transform;
        for (int i = 0; i < circlesPerFrame; i++)
        {
            lidar.DefaultScan(myTransform.forward, myTransform);
        }
        
    }
}
