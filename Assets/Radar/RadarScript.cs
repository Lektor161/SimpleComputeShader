using System;
using UnityEngine;

namespace Radar
{
    public class RadarScript : MonoBehaviour
    {
        [SerializeField]
        public Camera cam;
        [SerializeField]
        public GameObject radar;
        [SerializeField]
        public ComputeShader shader;
        [SerializeField]
        public float fragmentLength;
        [SerializeField]
        public float colorNormConst;

        private RenderTexture _colorTexture;
        private RenderTexture _depthTexture;
        private RenderTexture _radarTexture;
        private ComputeBuffer _buffer;

        private int _kernelID;
        private int _camWidth, _camHeight;
        private float _verAngle, _horAngle;
        private float _camNear, _camFar;
        private float _curAngle;

        private const int TextureWidth = 1024, TextureHeight = 1024;
        private int _fragmentNum;

        private int _generateBufferKernelID;
        private int _generateTextureKernelID;
        private int _clearBufferKernelID;

        void Start()
        {
            _generateBufferKernelID = shader.FindKernel("generate_buffer");
            _generateTextureKernelID = shader.FindKernel("generate_texture");
            _clearBufferKernelID = shader.FindKernel("clear_buffer");
        
            _camNear = cam.nearClipPlane;
            _camFar = cam.farClipPlane;
            _camWidth = cam.pixelWidth;
            _camHeight = cam.pixelHeight;
            _verAngle = cam.fieldOfView * Mathf.Deg2Rad;
            _horAngle = 2 * Mathf.Atan(Mathf.Tan(_verAngle * 0.5f) * cam.aspect);

            _curAngle = -_horAngle / 2;
        
            _fragmentNum = (int) Math.Ceiling(
                _camFar * Mathf.Sqrt(1 + Mathf.Pow(Mathf.Tan(_horAngle / 2), 2) + 
                                     Mathf.Pow(Mathf.Tan(_verAngle / 2), 2)) /
                fragmentLength);
            
            _colorTexture = CreateTexture(_camWidth, _camHeight, 0, RenderTextureFormat.ARGBFloat, false);
            _depthTexture = CreateTexture(_camWidth, _camHeight, 24, RenderTextureFormat.Depth, false);
            _radarTexture = CreateTexture(TextureWidth, TextureHeight, 0, RenderTextureFormat.ARGBFloat, true);
            cam.SetTargetBuffers(_colorTexture.colorBuffer, _depthTexture.depthBuffer);

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
            shader.SetFloat("fragment_length", fragmentLength);
            shader.SetInt("fragment_num", _fragmentNum);
            shader.SetFloat("max_radius", 0.5f * TextureWidth);
            shader.SetFloat("cur_angle", _curAngle);
            shader.SetInt("texture_width", TextureWidth);
            shader.SetInt("texture_height", TextureHeight);
            shader.SetFloat("color_norm_const", colorNormConst);
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            Dispatch(shader, _generateBufferKernelID, _camWidth, _camHeight);
            Dispatch(shader, _generateTextureKernelID, TextureWidth, TextureHeight);
            Dispatch(shader, _clearBufferKernelID, _camWidth, _fragmentNum);
            radar.GetComponent<MeshRenderer>().material.mainTexture = _radarTexture;
    
            cam.transform.Rotate(0, 1f, 0);
            _curAngle += 1f * Mathf.Deg2Rad;
            _curAngle %= 2 * Mathf.PI;
            shader.SetFloat("cur_angle", _curAngle);
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
