using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementBehaviour : MonoBehaviour
{
    public Transform CameraPosition;
    public float Speed = 6f;

    public void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 dir = (transform.position - CameraPosition.position).normalized;

        transform.position += dir * vertical * Speed * Time.deltaTime;
    }
}
