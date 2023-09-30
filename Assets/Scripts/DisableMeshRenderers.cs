using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableMeshRenderers : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        foreach (var childMeshRenderer in transform.GetComponentsInChildren<MeshRenderer>())
        {
            childMeshRenderer.enabled = false;
        }    
    }
}
