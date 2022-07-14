using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    Transform m_cameraTransform;

    [SerializeField]
    float m_moveSpeed;

    [SerializeField]
    Vector2 m_sensitivity;

    [SerializeField]
    Vector2 m_clampX;

    CharacterController m_characterController;

    void Awake()
    {
        m_characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        //move
        Vector3 move = Input.GetAxisRaw("Horizontal") * transform.right + Input.GetAxisRaw("Vertical") * transform.forward;

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        m_characterController.SimpleMove(move * m_moveSpeed);

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

        transform.Rotate(Vector3.up, horizontal * Time.deltaTime * m_sensitivity.x);
        
        float x = m_cameraTransform.localRotation.eulerAngles.x;
        if (x > 180f)
        {
            x -= 360f;
        }
        x -= vertical * Time.deltaTime * m_sensitivity.y;
        x = Mathf.Clamp(x, m_clampX.x, m_clampX.y);
        m_cameraTransform.localRotation = Quaternion.Euler(x, 0f, 0f);
    }
}
