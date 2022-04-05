using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpinningScript : MonoBehaviour
{
    [SerializeField] public GameObject obj;
    [SerializeField] public float radius;

    private float _angle = 0;
    
    void Update()
    {
        _angle += 0.01f;
        var x = radius * Mathf.Cos(_angle);
        var y = radius * Mathf.Sin(_angle);
        obj.transform.position = new Vector3(x, 0, y);
    }
}
