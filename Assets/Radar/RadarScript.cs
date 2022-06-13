using System;
using System.Drawing;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Radar
{
    public class RadarScript : MonoBehaviour
    {
        [SerializeField]
        public GameObject outputObject;
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
        public ComputeShader radarShader;
        [SerializeField]
        public ComputeShader effectsShader;
        [SerializeField]
        public ComputeShader bufferToTextureShader;
        
        [SerializeField]
        public float colorNormConst;
        [SerializeField] 
        public float noiseConst = 0.1f;
        
        private RenderTexture _colorTexture;
        private RenderTexture _depthTexture;
        private RenderTexture _bufferTexture;
        private RenderTexture _radarTexture;
        private RenderTexture _blurTexture;
        private ComputeBuffer _outputBuffer;
        private ComputeBuffer _inputBuffer;
        private RenderTexture _outputTexture;

        private Random _random = new Random();
        
        private int _camWidth;
        public int textureWidth = 1024;
        public int textureHeight = 1024;
        private int _bufferHeight;
        
        private float _bufAngle;
        private int _sectionCount, _curSection;
        
        private int _generateBufferKernelID;
        private int _clearBufferKernelID;
        private int _generateTextureKernelID;
        private int _blurKernelID;
        private int _bufferToTextureKernelID;

        private void RadarShaderStart()
        {
            _generateBufferKernelID = radarShader.FindKernel("generate_buffer");
            _clearBufferKernelID = radarShader.FindKernel("clear_buffer");
            _generateTextureKernelID = radarShader.FindKernel("generate_texture");
            radarShader.SetTexture(_generateTextureKernelID, "radar_texture", _radarTexture);
            
            radarShader.SetInt("texture_width", textureWidth);
            radarShader.SetInt("texture_height", textureHeight);
            radarShader.SetInt("x_shift", 0);
        }

        private void EffectsShaderStart()
        {
            _blurKernelID = effectsShader.FindKernel("blur");
            effectsShader.SetTexture(_blurKernelID, "radar_texture", _radarTexture);
            effectsShader.SetTexture(_blurKernelID, "blur_texture", _blurTexture);
            effectsShader.SetInt("texture_width", textureWidth);
        }

        private void BufferToTextureShaderStart()
        {
            _bufferToTextureKernelID = bufferToTextureShader.FindKernel("buffer_to_texture");
        }
        
        void Start()
        {
            _radarTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat, true);
            _blurTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat, true);
            
            RadarShaderStart();
            EffectsShaderStart();
            BufferToTextureShaderStart();
            
            OnValidate();
        }

        private void FixedUpdate()
        {
            if (!SpinCamera()) return;
            radarShader.SetInt("x_shift", _camWidth * _curSection);
            effectsShader.SetInt("x_shift", _camWidth * ((_curSection - 1 + _sectionCount) % _sectionCount));
            effectsShader.SetInt("random_seed", _random.Next());
            
            Dispatch(radarShader, _clearBufferKernelID, _camWidth, _bufferHeight);
            Dispatch(radarShader, _generateBufferKernelID, _camWidth, cameraHeight);
            Dispatch(radarShader, _generateTextureKernelID, _camWidth, textureHeight);
            Dispatch(effectsShader, _blurKernelID, _camWidth, textureHeight);
            radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _blurTexture);

            ExtractData();
        }

        private void ExtractData()
        {
            var outArray = new float[_camWidth * textureHeight];
            _outputBuffer.GetData(outArray);
            _inputBuffer.SetData(outArray);
            bufferToTextureShader.SetInt("x_shift", _camWidth * ((_curSection - 1 + _sectionCount) % _sectionCount));
            Dispatch(bufferToTextureShader, _bufferToTextureKernelID, _camWidth, textureHeight);
            outputObject.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _outputTexture);
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
        
        private void RadarTextureOnValidate()
        {
            var horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            var verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            radarShader.SetFloats("cam_angle", horAngle, verAngle);
            radarShader.SetFloat("color_norm_const", colorNormConst);
            radarShader.SetInt("buffer_height", _bufferHeight);
            radarShader.SetFloat("fragment_length", cameraFar / _bufferHeight);
            radarShader.SetFloat("near", cameraNear);
            radarShader.SetFloat("far", cameraFar);
            
            radarShader.SetTexture(_generateBufferKernelID, "buffer", _bufferTexture);
            radarShader.SetTexture(_clearBufferKernelID, "buffer", _bufferTexture);
            radarShader.SetTexture(_generateTextureKernelID, "buffer", _bufferTexture);
            
            radarShader.SetTexture(_generateBufferKernelID, "color_texture", _colorTexture);
            radarShader.SetTexture(_generateBufferKernelID, "depth_texture", _depthTexture);
            radarShader.SetInt("cam_width", _camWidth);
            radarShader.SetInt("cam_height", cameraHeight);
        }

        private void EffectsShaderOnValidate()
        {
            effectsShader.SetInts("blurxy", blurAngle, blurRadius);
            effectsShader.SetFloat("max_random_value", noiseConst);
            effectsShader.SetInt("cam_width", _camWidth);
        }
        
        private void BuffersOnValidate()
        {
            _outputBuffer?.Dispose();
            _outputBuffer = new ComputeBuffer(_camWidth * textureHeight, sizeof(float));
            _inputBuffer?.Dispose();
            _inputBuffer = new ComputeBuffer(_camWidth * textureHeight, sizeof(float));
            
            effectsShader.SetBuffer(_blurKernelID, "output_buffer", _outputBuffer);
            bufferToTextureShader.SetBuffer(_bufferToTextureKernelID, "buffer", _inputBuffer);
            bufferToTextureShader.SetTexture(_bufferToTextureKernelID, "tex", _outputTexture);
            bufferToTextureShader.SetInt("width", _camWidth);
        }
        
        private void CameraOnValidate()
        {
            _camWidth = textureWidth / _sectionCount;
            _colorTexture = CreateTexture(_camWidth, cameraHeight, 0, RenderTextureFormat.ARGBFloat, false);
            _depthTexture = CreateTexture(_camWidth, cameraHeight, 24, RenderTextureFormat.Depth, false);
            
            colorCam.targetTexture = _colorTexture;
            depthCam.targetTexture = _depthTexture;
            colorCam.enabled = false;
            depthCam.enabled = false;
            
            var horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            var verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            colorCam.aspect = depthCam.aspect = Mathf.Tan(horAngle / 2) / Mathf.Tan(verAngle / 2);
            colorCam.nearClipPlane = depthCam.nearClipPlane = cameraNear;
            colorCam.farClipPlane = depthCam.farClipPlane = cameraFar;
            colorCam.fieldOfView = depthCam.fieldOfView = cameraVerticalAngle;
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
            
            _outputTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat, true);
            _bufferTexture = CreateTexture(textureWidth, _bufferHeight, 0, RenderTextureFormat.RInt, true);
            CameraOnValidate();
            EffectsShaderOnValidate();
            RadarTextureOnValidate();
            BuffersOnValidate();
        }

        private void OnDisable()
        {
            _outputBuffer.Dispose();
            _inputBuffer.Dispose();
        }
    }
}