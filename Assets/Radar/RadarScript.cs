using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Radar
{
    public class RadarScript : MonoBehaviour
    {
        public GameObject cams;
        [SerializeField]
        public Camera colorCam;
        [SerializeField]
        public Camera depthCam;

        [SerializeField]
        public float cameraFar = 300f;
        [SerializeField]
        public float cameraNear = 0.3f;
        [SerializeField]
        public float cameraHorizontalAngle = 20;
        [SerializeField]
        public float cameraVerticalAngle = 60;

        [SerializeField]
        public int blurAngle;
        [SerializeField]
        public int blurRadius;
        [SerializeField]
        public float cameraRotationSpeed;
        
        [SerializeField]
        public GameObject radar;
        [SerializeField]
        public ComputeShader shader;

        private float _fragmentLength;
        [SerializeField]
        public float colorNormConst;

        private RenderTexture _colorTexture;
        private RenderTexture _depthTexture;
        private RenderTexture _radarTexture;
        private RenderTexture _blurTexture;
        private ComputeBuffer _buffer;

        private int _kernelID;
        private int _camWidth, _camHeight;
        private float _verAngle, _horAngle;

        public int textureWidth = 1024;
        public int textureHeight = 1024;
        
        private int _fragmentNum;
        private float _curAngle = 0;
        
        private int _generateBufferKernelID;
        private int _generateTextureKernelID;
        private int _clearBufferKernelID;
        private int _blurKernelID;

        void Start()
        {
            _generateBufferKernelID = shader.FindKernel("generate_buffer");
            _generateTextureKernelID = shader.FindKernel("generate_texture");
            _clearBufferKernelID = shader.FindKernel("clear_buffer");
            _blurKernelID = shader.FindKernel("blur");
            
            _camWidth = depthCam.pixelWidth;
            _camHeight = depthCam.pixelHeight;

            _colorTexture = CreateTexture(_camWidth, _camHeight, 0, RenderTextureFormat.ARGBFloat, false);
            _depthTexture = CreateTexture(_camWidth, _camHeight, 24, RenderTextureFormat.Depth, false);
            _radarTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            _blurTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            
            colorCam.targetTexture = _colorTexture;
            depthCam.targetTexture = _depthTexture;
            _buffer = new ComputeBuffer(_camWidth * _fragmentNum, sizeof(int));
        
            shader.SetTexture(_generateBufferKernelID, "color_texture", _colorTexture);
            shader.SetTexture(_generateBufferKernelID, "depth_texture", _depthTexture);
            shader.SetTexture(_generateTextureKernelID, "result_texture", _radarTexture);
            shader.SetBuffer(_generateBufferKernelID, "buffer", _buffer);
            shader.SetBuffer(_generateTextureKernelID, "buffer", _buffer);
            shader.SetBuffer(_clearBufferKernelID, "buffer", _buffer);
            
            shader.SetInt("cam_width", _camWidth);
            shader.SetInt("cam_height", _camHeight);
            shader.SetInt("texture_width", textureWidth);
            shader.SetInt("texture_height", textureHeight);
            shader.SetFloat("cur_angle", _curAngle);
            
            shader.SetTexture(_blurKernelID, "result_texture", _radarTexture);
            shader.SetTexture(_blurKernelID, "blur_texture", _blurTexture);
            colorCam.enabled = false;
            depthCam.enabled = false;
        }

        void FixedUpdate()
        {
            colorCam.Render();
            depthCam.Render();
            var angles = _horAngle / 2 / Mathf.PI * textureWidth;
            Dispatch(shader, _generateBufferKernelID, _camWidth, _camHeight);
            Dispatch(shader, _generateTextureKernelID, (int) angles, textureHeight);
            Dispatch(shader, _blurKernelID, (int) angles, textureHeight);
            Dispatch(shader, _clearBufferKernelID, _camWidth, textureHeight); 
            
            _curAngle += cameraRotationSpeed * Mathf.Deg2Rad * Time.deltaTime;
            shader.SetFloat("cur_angle", _curAngle % (2 * Mathf.PI));
            cams.transform.eulerAngles = new Vector3(0, _curAngle * Mathf.Rad2Deg, 0);
            
            radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _blurTexture);
        }
        private static void Dispatch(ComputeShader shader, int kernelID, int width, int height)
        {
            shader.Dispatch(kernelID, (width + 7) / 8, (height + 7) / 8, 1);
        }
        
        private static RenderTexture CreateTexture(int width, int height, int depth, RenderTextureFormat format, bool enableRw)
        {
            var texture = new RenderTexture(width, height, depth, format)
            {
                enableRandomWrite = enableRw
            };
            texture.Create();
            return texture;
        }

        
        public void OnValidate()
        {
            depthCam.nearClipPlane = cameraNear;
            depthCam.farClipPlane = cameraFar;
            colorCam.nearClipPlane = cameraNear;
            colorCam.farClipPlane = cameraFar;
            
            _verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            _horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            depthCam.fieldOfView = cameraVerticalAngle;
            depthCam.aspect = Mathf.Tan(_horAngle / 2) / Mathf.Tan(_verAngle / 2);
            colorCam.fieldOfView = cameraVerticalAngle;
            colorCam.aspect = Mathf.Tan(_horAngle / 2) / Mathf.Tan(_verAngle / 2);
            
            shader.SetFloat("color_norm_const", colorNormConst);
            shader.SetFloats("cam_angle", _horAngle, _verAngle);
            shader.SetInts("blurxy", blurAngle, blurRadius);
            
            _fragmentNum = textureHeight;
            _fragmentLength = cameraFar / _fragmentNum *
                              Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(_horAngle / 2), 2) + 
                                         Mathf.Pow(Mathf.Tan(_verAngle / 2), 2));
            shader.SetInt("fragment_num", _fragmentNum);
            shader.SetFloat("fragment_length", _fragmentLength);
            
            shader.SetFloat("near", cameraNear);
            shader.SetFloat("far", cameraFar);
        }
        
        private void OnDestroy()
        {
            _buffer.Dispose();
        }
    }
}