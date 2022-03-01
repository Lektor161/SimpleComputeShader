using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shader : MonoBehaviour
{
    public ComputeShader computeShader;
    public RenderTexture texture;
    public GameObject gameObject;

    private RenderTexture result;

    private int kernelID;
    void Start()
    {
        texture.enableRandomWrite = true;
        kernelID = computeShader.FindKernel("Compute");
        result = new RenderTexture(texture.width, texture.height, 16)
        {
            enableRandomWrite = true
        };
        result.Create();
        computeShader.SetTexture(kernelID, "Texture", texture);
        computeShader.SetTexture(kernelID, "Result", result);
    }

    void Update()
    {
        computeShader.Dispatch(kernelID, result.width / 8, result.height / 8, 1);
        gameObject.GetComponent<MeshRenderer>().material.mainTexture = result;
    }
}
