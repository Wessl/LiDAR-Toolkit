using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ApplyMeshCollidersToMeshRenderers : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        foreach (var childMeshRenderer in transform.GetComponentsInChildren<MeshRenderer>())
        {
            childMeshRenderer.transform.gameObject.AddComponent<MeshCollider>();
        } 
    }
}
