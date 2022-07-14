using System.Collections.Generic;
using UnityEngine;

public class ServerObjectGenerator : MonoBehaviour
{
    [SerializeField]
    List<ServerObject> m_prefabs;

    public ServerObject Generate(int type, Transform root)
    {
        if (type >= 0 && type < m_prefabs.Count)
        {
            return Instantiate(m_prefabs[type], root);
        }
        else
        {
            throw new System.Exception($"Prefab not found for type {type}!");
        }
    }
}
