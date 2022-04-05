using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [SerializeField]
    public ComputeShader shader;
    [SerializeField]
    public Material material;

    public GameObject gObj;
    
    public RenderTexture texture;

    private int _kernelID;
    
    // Start is called before the first frame update
    void Start()
    {
        texture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true
        };
        texture.Create();
        
        _kernelID = shader.FindKernel("CSMain");
        shader.SetTexture(_kernelID, "result_texture", texture);
    }

    // Update is called once per frame
    void Update()
    { 
        print("123");
        shader.Dispatch(_kernelID, 1024 / 8, 1024 / 8, 1);
        gObj.GetComponent<MeshRenderer>().material.SetTexture("Texture2D_6F610954", texture);
    }
}
