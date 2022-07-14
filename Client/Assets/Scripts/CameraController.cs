using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    float m_moveSpeed;

    [SerializeField]
    Vector2 m_sensitivity;

    void Update()
    {
        //move
        Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

        if (Input.GetKey(KeyCode.Q))
        {
            move.y -= 1f;
        }

        if (Input.GetKey(KeyCode.E))
        {
            move.y += 1f;
        }

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        move = transform.TransformDirection(move);

        transform.position += m_moveSpeed * Time.deltaTime * move;

        //view
        float horizontal = Input.GetAxis("Mouse X");
        float vertical = Input.GetAxis("Mouse Y");
        if (Input.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!Input.GetMouseButton(1))
        {
            horizontal = 0f;
            vertical = 0f;
        }

        Vector3 euler = transform.rotation.eulerAngles;

        euler.y += horizontal * Time.deltaTime * m_sensitivity.x;
        euler.x -= vertical * Time.deltaTime * m_sensitivity.y;
        euler.z = 0f;

        transform.rotation = Quaternion.Euler(euler);
    }
}
