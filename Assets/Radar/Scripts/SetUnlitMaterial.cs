using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class SetUnlitMaterial : MonoBehaviour
{
    public GameObject mainObject;
    public Shader shader;

    // Start is called before the first frame update
    void Start()
    {
        var random = new Random();
        var ts = mainObject.GetComponentsInChildren<Transform>();
        if (ts == null) return;
        var blueVal = 50;
        foreach (var t in ts)
        {
            blueVal = (blueVal + 1) % 256;
            blueVal = Math.Max(blueVal, 50);
            if (t == null || t.gameObject == null) continue;
            if (!t.gameObject.TryGetComponent(out Renderer render)) continue;
            var material = new Material(shader);
            //material.SetColor("_Color", Color.red + Color.green / 255 * blueVal);
            material.SetColor("_Color", Color.red + Color.green / 255 * (100 + random.Next(150)));
            render.material = material;
        }
    }

    private void RecSetUnlit(GameObject obj)
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
