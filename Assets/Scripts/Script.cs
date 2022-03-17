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
    private RenderTexture _result;
    private ComputeBuffer _computeBuffer;
    
    private int _kernelID;
    private int _clearID;
    private int _width;
    private int _height;
    private float _far;
    private float _near;
    private float _camAngle;
    
    private float _fragmentLength = 10;
    private int _fragmentNum;
    
    void Start()
    {
        _width = cam.pixelWidth;
        _height = cam.pixelHeight;
        _far = cam.farClipPlane;
        _near = cam.nearClipPlane;
        _camAngle = cam.fieldOfView;
        _fragmentNum = (int) Math.Ceiling(_far / Math.Pow(Math.Sin(_camAngle / 2 * Math.PI / 180), 2) / _fragmentLength) ;
        
        cam.depthTextureMode = DepthTextureMode.DepthNormals;
        _colorTexture = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGBFloat)
        {
            filterMode = FilterMode.Point
         };
        _colorTexture.Create();

        _depthTexture = new RenderTexture(_width, _height, 24, RenderTextureFormat.Depth);

        _result = new RenderTexture(_width, _height, 0)
        {
            enableRandomWrite = true
        };
        _result.Create();

        cam.SetTargetBuffers(_colorTexture.colorBuffer, _depthTexture.depthBuffer);
        
        _kernelID = computeShader.FindKernel("CSMain");
        _clearID = computeShader.FindKernel("Clear");
        computeShader.SetTexture(_kernelID, "depth_texture", _depthTexture);
        computeShader.SetTexture(_kernelID, "result", _result);
        computeShader.SetInt("width", _width);
        computeShader.SetInt("height", _height);
        computeShader.SetFloat("near", _near);
        computeShader.SetFloat("far", _far);
        computeShader.SetFloat("fragment_len", 10);
        computeShader.SetFloat("cam_angle", (float) Math.PI * cam.fieldOfView / 180);
        
        _computeBuffer = new ComputeBuffer(_width * _fragmentNum, sizeof(int));
        computeShader.SetBuffer(_kernelID, "buffer", _computeBuffer);
        
        computeShader.SetBuffer(_clearID, "buffer", _computeBuffer);
        gameObj.GetComponent<MeshRenderer>().material.mainTexture = _result;
    }

    void Update()
    {
        computeShader.Dispatch(_clearID, (_width + 7) / 8 , (_fragmentNum + 7) / 8, 1);
        computeShader.Dispatch(_kernelID, (_width + 7) / 8 , (_height + 7) / 8, 1);
        gameObj.GetComponent<MeshRenderer>().material.mainTexture = _result;
        print("1");
        
        var res = new int[_width * _fragmentNum];
        _computeBuffer.GetData(res);

        var sb = new StringBuilder();
        for (var i = 0; i < _fragmentNum; i++)
        {
            sb.Append(res[i * _width + 200]).Append(", ");
        }
        print(sb);
    }

    private void OnDestroy()
    {
        _computeBuffer.Dispose();
    }
}
