using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LiDAR))]
public class TurretScanner : MonoBehaviour
{
    [SerializeField] private float spinRate = 30f;
    [SerializeField] private TurretSpinType spinType;
    private LiDAR m_lidar;
    private LineRenderer m_lineRenderer;
    private Transform m_transform;


    private enum TurretSpinType
    {
        Circles,
        BackAndForth
    }

    private void Start()
    {
        m_lidar = GetComponent<LiDAR>();
        m_transform = GetComponent<Transform>();
        m_lineRenderer = GetComponent<LineRenderer>();
        m_lidar.scanType = LiDAR.ScanType.Line;
    }

    // Update is called once per frame
    void Update()
    {
        m_lineRenderer.positionCount = 0;    // Clear each frame
        switch (spinType)
        {
            case TurretSpinType.Circles:
                CircleSpin();
                break;
            case TurretSpinType.BackAndForth:
            default:
                throw new NotImplementedException();
                break;
        }
        
        m_lidar.DefaultScan(m_transform.forward, m_transform);
    }

    private void CircleSpin()
    {
        m_transform.Rotate(Vector3.up, spinRate * Time.deltaTime);
    }
}
