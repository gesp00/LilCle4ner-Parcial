using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Transform target; 
    [SerializeField] private Vector3 offset = new Vector3(10f, 10f, -10f); 
    [SerializeField] private float smoothSpeed = 5f; 

    void LateUpdate()
    {
        if (target == null) return;

        
        Vector3 desiredPosition = target.position + offset;

        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        transform.position = smoothedPosition;

        
    }
}
