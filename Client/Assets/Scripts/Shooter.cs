using System.Collections.Generic;
using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField]
    List<Rigidbody> m_prefabs;

    [SerializeField]
    float m_spawnDistance;

    [SerializeField]
    float m_destroyTime;

    [SerializeField]
    float m_force;

    [SerializeField]
    Vector3 m_torque;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && m_prefabs.Count > 0)
        {
            int idx = Random.Range(0, m_prefabs.Count);
            Rigidbody rb = Instantiate(m_prefabs[idx], transform.position + transform.forward * m_spawnDistance, Quaternion.identity);
            rb.AddForce(transform.forward * m_force);
            rb.AddTorque(m_torque);
            Destroy(rb.gameObject, m_destroyTime);
        }
    }
}
