using UnityEngine;

public class FreeCamerBehaviour : MonoBehaviour
{
    public float Speed = 2f;

    public void Update()
    {
        var vert = Input.GetAxis("Vertical");
        var hor = Input.GetAxis("Horizontal");

        var dir = transform.forward * vert + transform.right * hor;

        transform.position += dir * Time.deltaTime * Speed;
    }
}
