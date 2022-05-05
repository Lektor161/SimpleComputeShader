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
        [SerializeField]
        public float delta;
        [SerializeField]
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
        public int bufferHeight;
        
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
        private RenderTexture _bufferTexture;
        
        private int _kernelID;
        private int _camWidth, _camHeight;

        public int textureWidth = 1024;
        public int textureHeight = 1024;
        
        private float _bufAngle = 0f;
        private int _sectionCount, _curSection = 0;
        
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
            _bufferTexture = CreateTexture(_camWidth,bufferHeight, 0, RenderTextureFormat.RInt, true);
            
            shader.SetTexture(_generateBufferKernelID, "color_texture", _colorTexture);
            shader.SetTexture(_generateBufferKernelID, "depth_texture", _depthTexture);
            shader.SetTexture(_generateTextureKernelID, "result_texture", _radarTexture);
            shader.SetTexture(_generateBufferKernelID, "buffer", _bufferTexture);
            shader.SetTexture(_generateTextureKernelID, "buffer", _bufferTexture);
            shader.SetTexture(_clearBufferKernelID, "buffer", _bufferTexture);
            
            shader.SetInt("cam_width", _camWidth);
            shader.SetInt("cam_height", _camHeight);
            shader.SetInt("texture_width", textureWidth);
            shader.SetInt("texture_height", textureHeight);
            shader.SetInt("x_shift", 0);
            
            shader.SetTexture(_blurKernelID, "result_texture", _radarTexture);
            shader.SetTexture(_blurKernelID, "blur_texture", _blurTexture);
            colorCam.enabled = false;
            depthCam.enabled = false;
        }

        void FixedUpdate()
        {
            if (!SpinCamera()) return;
            Dispatch(shader, _generateBufferKernelID, _camWidth, _camHeight);
            var xWidth = textureWidth / _sectionCount / 8 * 8;
            shader.SetInt("x_shift", xWidth * _curSection);
            if (_curSection + 1 == _sectionCount)
            {
                xWidth = textureWidth - (_sectionCount - 1) * xWidth;
            }
            Dispatch(shader, _generateTextureKernelID, xWidth, textureHeight);

            Dispatch(shader, _clearBufferKernelID, _camWidth, textureHeight);  
            radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _radarTexture);
        }
        
        private bool SpinCamera()
        {
            _bufAngle += cameraRotationSpeed * Time.deltaTime;
            if (_bufAngle * _sectionCount < 360f) return false;
            _bufAngle -= 360f / _sectionCount;
            _curSection = (_curSection + 1) % _sectionCount;
            cams.transform.eulerAngles = new Vector3(0, 360f * _curSection / _sectionCount);
            colorCam.Render();
            depthCam.Render();
            return true;
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
            cameraRotationSpeed = Mathf.Min(cameraRotationSpeed, cameraHorizontalAngle / Time.deltaTime);

            depthCam.nearClipPlane = cameraNear;
            depthCam.farClipPlane = cameraFar;
            colorCam.nearClipPlane = cameraNear;
            colorCam.farClipPlane = cameraFar;
            depthCam.fieldOfView = cameraVerticalAngle;
            colorCam.fieldOfView = cameraVerticalAngle;

            var horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            var verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            depthCam.aspect = Mathf.Tan(horAngle / 2) / Mathf.Tan(verAngle / 2);
            colorCam.aspect = Mathf.Tan(horAngle / 2) / Mathf.Tan(verAngle / 2);
            
            shader.SetFloat("color_norm_const", colorNormConst);
            shader.SetFloats("cam_angle", horAngle, verAngle);
            shader.SetInts("blurxy", blurAngle, blurRadius);
            
            _fragmentLength = cameraFar / bufferHeight *
                              Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(horAngle / 2), 2) + 
                                         Mathf.Pow(Mathf.Tan(verAngle / 2), 2));
            shader.SetInt("fragment_num", bufferHeight);
            shader.SetFloat("fragment_length", Mathf.Sqrt(_fragmentLength));

            
            shader.SetFloat("near", cameraNear);
            shader.SetFloat("far", cameraFar);
            shader.SetInt("fragment_count", bufferHeight);
            
            _sectionCount = 1;
            while (_sectionCount < 32 && 360f / _sectionCount > cameraHorizontalAngle)
            {
                _sectionCount *= 2;
            }
            shader.SetInt("x_width", textureWidth / _sectionCount);
            
            shader.SetFloat("delta", delta);

            if (_bufferTexture != null && _bufferTexture.height != bufferHeight)
            {
                _bufferTexture = CreateTexture(_camWidth,bufferHeight, 0, RenderTextureFormat.RInt, true);
                shader.SetTexture(_generateBufferKernelID, "buffer", _bufferTexture);
                shader.SetTexture(_generateTextureKernelID, "buffer", _bufferTexture);
                shader.SetTexture(_clearBufferKernelID, "buffer", _bufferTexture);
            }
        }
    }
}