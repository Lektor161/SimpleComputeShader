using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Script : MonoBehaviour
{
    [SerializeField]
    public ComputeShader computeShader;
    [SerializeField]
    public RenderTexture texture;
    [SerializeField]
    public GameObject gameObj;

    private RenderTexture _result;

    private int _kernelID;
    void Start()
    {
        _kernelID = computeShader.FindKernel("Compute");
        _result = new RenderTexture(texture.width, texture.height, 16)
        {
            enableRandomWrite = true
        };
        _result.Create();
        computeShader.SetTexture(_kernelID, "Texture", texture);
        computeShader.SetTexture(_kernelID, "Result", _result);
        gameObj.GetComponent<MeshRenderer>().material.mainTexture = _result;
    }

    void FixedUpdate()
    {
        computeShader.Dispatch(_kernelID, _result.width / 8, _result.height / 8, 1);
    }
}
