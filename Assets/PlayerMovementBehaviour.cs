using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementBehaviour : MonoBehaviour
{
    public Transform CameraPosition;
    public float Speed = 0.5f;

    private Rigidbody _rigidBody;
    private Vector3 _direction;
    private float _vertical;

    public void Start()
    {
        _rigidBody = GetComponent<Rigidbody>();
    }

    public void Update()
    {
        _vertical = Input.GetAxis("Vertical");
        _direction = (transform.position - CameraPosition.position).normalized;
    }

    public void FixedUpdate()
    {
        _rigidBody.MovePosition(transform.position + _direction * _vertical * Speed);
    }
}
