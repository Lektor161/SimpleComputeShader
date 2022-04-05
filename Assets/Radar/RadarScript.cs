using System;
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
        public float cameraHorizontalAngle = 20;
        [SerializeField]
        public float cameraVerticalAngle = 60;
        
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
        private float _camNear, _camFar;

        public int textureWidth = 1024;
        public int textureHeight = 1024;
        public int blurRadius = 1;
        
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
        
            print(depthCam.fieldOfView);
            _camNear = depthCam.nearClipPlane;
            _camFar = depthCam.farClipPlane;
            _camWidth = depthCam.pixelWidth;
            _camHeight = depthCam.pixelHeight;

            _verAngle = cameraVerticalAngle * Mathf.Deg2Rad;
            _horAngle = cameraHorizontalAngle * Mathf.Deg2Rad;
            depthCam.fieldOfView = cameraVerticalAngle;
            depthCam.aspect = Mathf.Tan(_horAngle / 2) / Mathf.Tan(_verAngle / 2);
            colorCam.fieldOfView = cameraVerticalAngle;
            colorCam.aspect = Mathf.Tan(_horAngle / 2) / Mathf.Tan(_verAngle / 2);
            
            
            //_verAngle = depthCam.fieldOfView * Mathf.Deg2Rad;
            //_horAngle = 2 * Mathf.Atan(Mathf.Tan(_verAngle * 0.5f) * depthCam.aspect);

            _fragmentNum = textureHeight;

            _fragmentLength = _camFar / _fragmentNum *
                Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(_horAngle / 2), 2) + 
                           Mathf.Pow(Mathf.Tan(_verAngle / 2), 2));

            _colorTexture = CreateTexture(_camWidth, _camHeight, 0, RenderTextureFormat.ARGBFloat, false);
            _depthTexture = CreateTexture(_camWidth, _camHeight, 24, RenderTextureFormat.Depth, false);
            _radarTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            _blurTexture = CreateTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            
            colorCam.targetTexture = _colorTexture;
            depthCam.targetTexture = _depthTexture;
            //cam.SetTargetBuffers(_colorTexture.colorBuffer, _depthTexture.depthBuffer);

            _buffer = new ComputeBuffer(_camWidth * _fragmentNum, sizeof(int));
        
            shader.SetTexture(_generateBufferKernelID, "color_texture", _colorTexture);
            shader.SetTexture(_generateBufferKernelID, "depth_texture", _depthTexture);
            shader.SetTexture(_generateTextureKernelID, "result_texture", _radarTexture);
            shader.SetBuffer(_generateBufferKernelID, "buffer", _buffer);
            shader.SetBuffer(_generateTextureKernelID, "buffer", _buffer);
            shader.SetBuffer(_clearBufferKernelID, "buffer", _buffer);
        
            shader.SetFloat("near", _camNear);
            shader.SetFloat("far", _camFar);
            shader.SetFloats("cam_angle", _horAngle, _verAngle);
            shader.SetInt("cam_width", _camWidth);
            shader.SetInt("cam_height", _camHeight);
            shader.SetFloat("fragment_length", _fragmentLength);
            shader.SetInt("fragment_num", _fragmentNum);
            shader.SetFloat("max_radius", 0.5f * textureWidth);
            shader.SetInt("texture_width", textureWidth);
            shader.SetInt("texture_height", textureHeight);
            shader.SetFloat("color_norm_const", colorNormConst);
            shader.SetFloat("cur_angle", _curAngle);
            
            shader.SetTexture(_blurKernelID, "result_texture", _radarTexture);
            shader.SetTexture(_blurKernelID, "blur_texture", _blurTexture);
            shader.SetInt("blur_radius", blurRadius);
        }

        void FixedUpdate()
        {
            var angles = _horAngle / 2 / Mathf.PI * textureWidth;
            Dispatch(shader, _generateBufferKernelID, _camWidth, _camHeight);
            Dispatch(shader, _generateTextureKernelID, (int) angles, textureHeight);
            Dispatch(shader, _blurKernelID, (int) angles, textureHeight);
            Dispatch(shader, _clearBufferKernelID, _camWidth, textureHeight); 
            
            cams.transform.Rotate(0, _horAngle / 2 * Mathf.Rad2Deg, 0);
            _curAngle += _horAngle / 2;
            shader.SetFloat("cur_angle", _curAngle % (2 * Mathf.PI));
            
            //radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _radarTexture);
            radar.GetComponent<MeshRenderer>().material.SetTexture("_Texture", _blurTexture);
        }
        private void Dispatch(ComputeShader shader, int kernelID, int width, int height)
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

        private void OnDestroy()
        {
            _buffer.Dispose();
        }
    }
}