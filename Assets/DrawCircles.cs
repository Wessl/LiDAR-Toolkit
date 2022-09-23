using UnityEngine;
public class DrawCircles : MonoBehaviour
{
    public Shader shader;
    protected Material material;
    void Awake()
    {
        material = new Material(shader);
        float[] bufferX = new float[2048];
        float[] bufferY = new float[2048];
        for (int i=0; i<2048; i++)
        {
            bufferX[i] = Random.Range(0.0f, 120.0f);
            bufferY[i] = Random.Range(0.0f, 120.0f);
        }
        material.SetFloatArray("BufferX", bufferX);
        material.SetFloatArray("BufferY", bufferY);
    }
    void OnRenderObject()
    {
        material.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, 2048);
    }
}