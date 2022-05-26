using System;
using System.Linq;
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
        public ComputeShader cameraShader;
        [SerializeField]
        public ComputeShader textureShader;

        private float _fragmentLength;
        [SerializeField]
        public float colorNormConst;

        private RenderTexture _colorTexture;
        private RenderTexture _depthTexture;
        private RenderTexture _bufferTexture;
        private RenderTexture _radarTexture;
        private RenderTexture _blurTexture;
        private ComputeBuffer _outputBuffer;
        
        private int _camWidth;
        public int textureWidth = 1024;
        public int textureHeight = 1024;
        
        private float _bufAngle;
        private int _sectionCount, _curSection;
        
        private int _generateBufferKernelID;
        private int _clearBufferKernelID;
        private int _generateTextureKernelID;
        private int _blurKernelID;
        private int _bufferHeight;
        

        void Start()
        {
            _generateBufferKernelID = cameraShader.FindKernel("generate_buffer");
            _clearBufferKernelID = cameraShader.FindKernel("clear_buffer");
            _generateTextureKernelID = textureShader.FindKernel("generate_texture");
            _blurKernelID = textureShader.FindKernel("blur");
            
            _bufferTexture = CreateTexture(textureWidth, _bufferHeight, 0, RenderTextureFormat.RInt, true);
            _radarTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat, true);
            _blurTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat, true);

            cameraShader.SetTexture(_generateBufferKernelID, "buffer", _bufferTexture);
            cameraShader.SetTexture(_clearBufferKernelID, "buffer", _bufferTexture);

            textureShader.SetTexture(_generateTextureKernelID, "buffer", _bufferTexture);
            textureShader.SetTexture(_generateTextureKernelID, "radar_texture", _radarTexture);
            textureShader.SetTexture(_blurKernelID, "radar_texture", _radarTexture);
            textureShader.SetTexture(_blurKernelID, "blur_texture", _blurTexture);
            textureShader.SetTexture(_blurKernelID, "buffer", _bufferTexture);
            
            textureShader.SetInt("texture_width", textureWidth);
            textureShader.SetInt("texture_height", textureHeight);
            textureShader.SetInt("x_shift", 0);
            cameraShader.SetInt("x_shift", 0);
            
            OnValidate();
        }

        private void FixedUpdate()
        {
            if (!SpinCamera()) return;
            cameraShader.SetInt("x_shift", _camWidth * _curSection);
            textureShader.SetInt("x_shift", _camWidth * ((_curSection + _sectionCount - 1) % _sectionCount));
            
            Dispatch(cameraShader, _clearBufferKernelID, _camWidth, _bufferHeight);
            Dispatch(cameraShader, _generateBufferKernelID, _camWidth, cameraHeight);
            Dispatch(textureShader, _generateTextureKernelID, _camWidth, textureHeight);
            Dispatch(textureShader, _blurKernelID, _camWidth, textureHeight);
            radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _blurTexture);

            var outArray = new float[_camWidth * textureHeight];
            _outputBuffer.GetData(outArray);
            print(outArray.Sum());
        }
        
        private bool SpinCamera()
        {
            _bufAngle += cameraRotationSpeed * Time.deltaTime;
            _bufAngle %= 10000;
            if (_bufAngle * _sectionCount < 360f) return false;
            _bufAngle -= 360f / _sectionCount;
            _curSection = (_curSection + 1) % _sectionCount;
            cams.transform.eulerAngles = new Vector3(0, (180 + 360f * _curSection) / _sectionCount);
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
            _sectionCount = 1;
            while (_sectionCount < 32 && 360f / _sectionCount > cameraHorizontalAngle)
            {
                _sectionCount *= 2;
            }
            cameraHorizontalAngle = 360f / _sectionCount;
            _bufferHeight = textureHeight;

            colorCam.nearClipPlane = depthCam.nearClipPlane = cameraNear;
            colorCam.farClipPlane = depthCam.farClipPlane = cameraFar;
            colorCam.fieldOfView = depthCam.fieldOfView = cameraVerticalAngle;

            var horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            var verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            colorCam.aspect = depthCam.aspect = Mathf.Tan(horAngle / 2) / Mathf.Tan(verAngle / 2);
            
            textureShader.SetFloat("color_norm_const", colorNormConst);
            cameraShader.SetFloats("cam_angle", horAngle, verAngle);
            textureShader.SetFloats("cam_angle", horAngle, verAngle);

            textureShader.SetInts("blurxy", blurAngle, blurRadius);
            
            _fragmentLength = cameraFar / _bufferHeight *
                              Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(horAngle / 2), 2) + 
                                         Mathf.Pow(Mathf.Tan(verAngle / 2), 2));
            textureShader.SetInt("buffer_height", _bufferHeight);
            cameraShader.SetFloat("fragment_length", _fragmentLength);
            
            cameraShader.SetFloat("near", cameraNear);
            cameraShader.SetFloat("far", cameraFar);
            UpdateCamera();

            _outputBuffer?.Dispose();
            _outputBuffer = new ComputeBuffer(_camWidth * textureHeight, sizeof(float));
            textureShader.SetBuffer(_blurKernelID, "output_buffer", _outputBuffer);
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
            
            cameraShader.SetTexture(_generateBufferKernelID, "color_texture", _colorTexture);
            cameraShader.SetTexture(_generateBufferKernelID, "depth_texture", _depthTexture);
            cameraShader.SetInt("cam_width", _camWidth);
            cameraShader.SetInt("cam_height", cameraHeight);
            
            textureShader.SetInt("cam_width", _camWidth);
            textureShader.SetInt("cam_height", cameraHeight);
        }

        private void OnDestroy()
        {
            _outputBuffer.Dispose();
        }
    }
}