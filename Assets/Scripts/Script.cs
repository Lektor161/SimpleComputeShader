using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class Script : MonoBehaviour
{
    [SerializeField]
    public Camera cam;
    [SerializeField]
    public ComputeShader computeShader;
    [SerializeField]
    public GameObject gameObj;
    
    
    private RenderTexture _colorTexture;
    private RenderTexture _depthTexture;
    public RenderTexture _result;
    private ComputeBuffer _computeBuffer;
    
    private int _kernelID;
    private int _clearID;
    private int _getTextureID;
    
    private int _width;
    private int _height;
    private float _far;
    private float _near;
    private float _horAngle;
    private float _verAngle;

    private const float FragmentLength = 1;
    private int _fragmentNum;
    
    void Start()
    {
        _width = cam.pixelWidth;
        _height = cam.pixelHeight;
        _far = cam.farClipPlane;
        _near = cam.nearClipPlane;
        _verAngle = cam.fieldOfView * Mathf.Deg2Rad;
        _horAngle = 2 * Mathf.Atan(Mathf.Tan(_verAngle * 0.5f) * cam.aspect);
        
        print(_verAngle * Mathf.Rad2Deg + " " + _horAngle * Mathf.Rad2Deg);
        
        _fragmentNum = (int) Math.Ceiling(
            _far * Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(_horAngle / 2), 2) + 
                                   Mathf.Pow(Mathf.Tan(_verAngle / 2), 2)) /
            FragmentLength);
        print("height: " + _height);
        print("frag_num: " + _fragmentNum);
        
        cam.depthTextureMode = DepthTextureMode.DepthNormals;
        _colorTexture = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGBFloat)
        {
            filterMode = FilterMode.Point
        };
        _colorTexture.Create();

        _depthTexture = new RenderTexture(_width, _height, 24, RenderTextureFormat.Depth);

        _result = new RenderTexture(_width, _fragmentNum, 0)
        {
            enableRandomWrite = true
        };
        _result.Create();

        cam.SetTargetBuffers(_colorTexture.colorBuffer, _depthTexture.depthBuffer);
        
        _kernelID = computeShader.FindKernel("Compute");
        _clearID = computeShader.FindKernel("Clear");
        _getTextureID = computeShader.FindKernel("GetTexture");
        
        computeShader.SetTexture(_kernelID, "color_texture", _colorTexture);
        computeShader.SetTexture(_getTextureID, "color_texture", _colorTexture);

        computeShader.SetTexture(_kernelID, "depth_texture", _depthTexture);
        computeShader.SetTexture(_kernelID, "result", _result);
        computeShader.SetTexture(_getTextureID, "result", _result);
        computeShader.SetInt("width", _width);
        computeShader.SetInt("height", _height);
        computeShader.SetFloat("fragment_len", FragmentLength);
        computeShader.SetInt("fragment_num", _fragmentNum);
        computeShader.SetFloat("near", _near);
        computeShader.SetFloat("far", _far);
        computeShader.SetFloats("cam_angle", _horAngle, _verAngle);

        _computeBuffer = new ComputeBuffer(_width * _fragmentNum, sizeof(uint));
        computeShader.SetBuffer(_kernelID, "buffer", _computeBuffer);
        computeShader.SetBuffer(_clearID, "buffer", _computeBuffer);
        computeShader.SetBuffer(_getTextureID,"buffer", _computeBuffer);
        gameObj.GetComponent<MeshRenderer>().material.mainTexture = _result;
    }

    void Update()
    {
        Dispatch(_clearID, _width, _height);
        Dispatch(_kernelID, _width, _height);
        Dispatch(_getTextureID, _width, _fragmentNum);
        gameObj.GetComponent<MeshRenderer>().material.mainTexture = _result;
    }

    private void Dispatch(int kernel, int width, int height)
    {
        computeShader.Dispatch(kernel, (width + 7) / 8, (height + 7) / 8, 1);
    }

    private void OnDestroy()
    {
        _computeBuffer.Dispose();
    }
}
