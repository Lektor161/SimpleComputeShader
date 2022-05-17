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
        public GameObject cams;
        [SerializeField]
        public Camera colorCam;
        [SerializeField]
        public Camera depthCam;

        [SerializeField]
        public int cameraHeight = 1000;
        [SerializeField]
        public float cameraFar = 1000f;
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
        private RenderTexture _bufferTexture;
        
        private int _camWidth;
        public int textureWidth = 1024;
        public int textureHeight = 1024;
        
        private float _bufAngle;
        private int _sectionCount, _curSection;
        
        private int _generateBufferKernelID;
        private int _generateTextureKernelID;
        private int _clearBufferKernelID;
        private int _blurKernelID;
        
        private int _bufferHeight;
        private Texture2D _ramTexture;
        
        void Start()
        {
            _generateBufferKernelID = shader.FindKernel("generate_buffer");
            _generateTextureKernelID = shader.FindKernel("generate_texture");
            _clearBufferKernelID = shader.FindKernel("clear_buffer");
            _blurKernelID = shader.FindKernel("blur");
            
            _radarTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            _blurTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            
            shader.SetTexture(_generateTextureKernelID, "result_texture", _radarTexture);
            shader.SetTexture(_blurKernelID, "result_texture", _radarTexture);
            shader.SetTexture(_blurKernelID, "blur_texture", _blurTexture);

            shader.SetInt("texture_width", textureWidth);
            shader.SetInt("texture_height", textureHeight);
            shader.SetInt("x_shift", 0);
            
            _ramTexture = new Texture2D(_blurTexture.width, _blurTexture.height);
            OnValidate();
        }

        private void FixedUpdate()
        {
            if (!SpinCamera()) return;
            shader.SetInt("x_shift", _camWidth * _curSection);
            Dispatch(shader, _generateBufferKernelID, _camWidth, cameraHeight);
            Dispatch(shader, _generateTextureKernelID, _camWidth, textureHeight);
            Dispatch(shader, _clearBufferKernelID, _camWidth, _bufferHeight); 
            Dispatch(shader, _blurKernelID, _camWidth, textureHeight);
            radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _blurTexture);

            RenderTexture.active = _blurTexture;
            _ramTexture.ReadPixels(new Rect(0, 0, _ramTexture.width, _ramTexture.height), 0, 0);
        }
        
        private bool SpinCamera()
        {
            _bufAngle += cameraRotationSpeed * Time.deltaTime;
            if (_bufAngle * _sectionCount < 360f) return false;
            _bufAngle -= 360f / _sectionCount;
            _curSection = (_curSection + 1) % _sectionCount;
            cams.transform.eulerAngles = new Vector3(0, (180f + 360f * _curSection) / _sectionCount);
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
            _bufferHeight = textureHeight;
            cameraRotationSpeed = Mathf.Min(cameraRotationSpeed, cameraHorizontalAngle / Time.deltaTime);

            colorCam.nearClipPlane = depthCam.nearClipPlane = cameraNear;
            colorCam.farClipPlane = depthCam.farClipPlane = cameraFar;
            colorCam.fieldOfView = depthCam.fieldOfView = cameraVerticalAngle;

            var horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            var verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            colorCam.aspect = depthCam.aspect = Mathf.Tan(horAngle / 2) / Mathf.Tan(verAngle / 2);
            
            shader.SetFloat("color_norm_const", colorNormConst);
            shader.SetFloats("cam_angle", horAngle, verAngle);
            shader.SetInts("blurxy", blurAngle, blurRadius);
            
            _fragmentLength = cameraFar / _bufferHeight *
                              Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(horAngle / 2), 2) + 
                                         Mathf.Pow(Mathf.Tan(verAngle / 2), 2));
            shader.SetInt("buffer_height", _bufferHeight);
            shader.SetFloat("fragment_length", _fragmentLength);
            
            shader.SetFloat("near", cameraNear);
            shader.SetFloat("far", cameraFar);
            
            var sectionCount = 1;
            while (sectionCount < 32 && 360f / sectionCount > cameraHorizontalAngle)
            {
                sectionCount *= 2;
            }

            _sectionCount = sectionCount; 
            UpdateCamera(); 
            UpdateBuffer();
            shader.SetInt("x_width", _camWidth);
        }

        private void UpdateCamera()
        {
            _camWidth = textureWidth / _sectionCount;
            _colorTexture = CreateTexture(_camWidth, cameraHeight, 0, RenderTextureFormat.ARGBFloat, false);
            _depthTexture = CreateTexture(_camWidth, cameraHeight, 24, RenderTextureFormat.Depth, false);
            
            colorCam.targetTexture = _colorTexture;
            depthCam.targetTexture = _depthTexture;
            colorCam.enabled = false;
            depthCam.enabled = false;
            
            shader.SetTexture(_generateBufferKernelID, "color_texture", _colorTexture);
            shader.SetTexture(_generateBufferKernelID, "depth_texture", _depthTexture);
            shader.SetInt("cam_width", _camWidth);
            shader.SetInt("cam_height", cameraHeight);
        }

        private void UpdateBuffer()
        {
            _bufferTexture = CreateTexture(_camWidth, _bufferHeight, 0, RenderTextureFormat.RInt, true);
            shader.SetTexture(_generateBufferKernelID, "buffer", _bufferTexture);
            shader.SetTexture(_generateTextureKernelID, "buffer", _bufferTexture);
            shader.SetTexture(_clearBufferKernelID, "buffer", _bufferTexture);
        }
    }
}