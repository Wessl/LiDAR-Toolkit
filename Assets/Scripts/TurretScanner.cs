using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiDAR))]
public class TurretScanner : MonoBehaviour
{
    public float spinRate = 30f;
    private LiDAR lidar;
    private LineRenderer lineRenderer;

    private void Start()
    {
        lidar = GetComponent<LiDAR>();
        lineRenderer = GetComponent<LineRenderer>();
        lidar.scanType = LiDAR.ScanType.Line;
    }

    // Update is called once per frame
    void Update()
    {
        lineRenderer.positionCount = 0;    // Clear each frame
        Transform myTransform = this.transform;
        myTransform.Rotate(Vector3.right, spinRate * Time.deltaTime);
        lidar.DefaultScan(myTransform.forward, myTransform);
    }
}
