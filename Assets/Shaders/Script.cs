using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Script : MonoBehaviour
{
    public Camera myCamera;
    [SerializeField]
    public ComputeShader computeShader;
    [SerializeField]
    public GameObject gameObj;

    private RenderTexture _source;
    private RenderTexture _depth;
    private RenderTexture _result;
    private ComputeBuffer _computeBuffer;
    
    private int _width, _height;
    private int _kernelID;

    private float _nearClipPlane;
    private float _farClipPlane;
    
    void Start()
    {
        _nearClipPlane = myCamera.nearClipPlane;
        _farClipPlane = myCamera.farClipPlane;
        _width = myCamera.pixelWidth;
        _height = myCamera.pixelHeight;
        _computeBuffer = new ComputeBuffer(_width * _height, sizeof(float));

        print("width: " + _width + ", height: " + _height);
        print("near: " + myCamera.nearClipPlane + ", far: " + myCamera.farClipPlane);
        
        
        myCamera.depthTextureMode = DepthTextureMode.Depth;

        _source = new RenderTexture(_width, _height, 0, RenderTextureFormat.Default);
        _depth = new RenderTexture(_width, _height, 8, RenderTextureFormat.Depth);
        _result = new RenderTexture(_width, _height, 8);
        _result.enableRandomWrite = true;
        _result.Create();
        
        myCamera.SetTargetBuffers(_source.colorBuffer, _depth.depthBuffer);
        
        computeShader.SetBuffer(_kernelID, "buffer", _computeBuffer);
        computeShader.SetTexture(_kernelID, "Texture", _depth);
        computeShader.SetTexture(_kernelID, "Result", _result);
        computeShader.SetFloat("near_clip_plane", myCamera.nearClipPlane);
        computeShader.SetFloat("far_clip_plane", myCamera.farClipPlane);
        computeShader.SetFloat("width", _width);
        computeShader.SetFloat("height", _height);
    }

    void Update()
    {
        computeShader.Dispatch(_kernelID, _width / 8, _height / 8, 1);
        gameObj.GetComponent<MeshRenderer>().material.mainTexture = _result;

        var res = new float[_width * _height];
        _computeBuffer.GetData(res);
        var maxVal = float.MinValue;
        var minVal = float.MaxValue;
        
        foreach (var it in res)
        {
            if (it <= 1e-8)
            {
                continue;
            }
            maxVal = Math.Max(maxVal, it);
            minVal = Math.Min(minVal, it);
        }
        print("max: " + maxVal + ", min: " + minVal);
        print(LinearToDepth(maxVal) + " " + LinearToDepth(minVal));
    }
    
    float LinearToDepth(float linearDepth)
    {
        return ((float)1.0 - _nearClipPlane * linearDepth) / (linearDepth * _farClipPlane);
    }
}
