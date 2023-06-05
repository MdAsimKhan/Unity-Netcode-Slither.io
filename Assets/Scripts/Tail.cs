using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tail : MonoBehaviour
{
    public Transform networkOwner;
    public Transform followTransform;
    [SerializeField] private float dsistance = 0.3f;
    [SerializeField] private float delay = 0.1f;
    [SerializeField] private float step = 10f;
    private Vector3 _targetPosition;

    void Update()
    {
        _targetPosition = followTransform.position - followTransform.forward * dsistance;
        _targetPosition += (transform.position - _targetPosition) * delay;
        _targetPosition.z = 0f;
        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * step);
    }
}
