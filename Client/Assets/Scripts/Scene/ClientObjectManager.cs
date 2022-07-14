using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ClientObjectManager : MonoSingleton<ClientObjectManager>
{
    readonly object m_idLock = new object();
    int m_nextId = 1;

    readonly Dictionary<int, ClientObject> m_objects = new Dictionary<int, ClientObject>();

    public IReadOnlyDictionary<int, ClientObject> Objects => m_objects;

    public void RegisterObject(ClientObject obj)
    {
        if (obj.Id != 0 && m_objects.ContainsKey(obj.Id))
        {
            Debug.LogError("Object already registered!");
        }

        lock (m_idLock)
        {
            obj.Id = m_nextId;
            m_objects.Add(m_nextId, obj);

            m_nextId++;
        }
    }

    public void UnregisterObject(ClientObject obj)
    {
        if (obj.Id == 0 || !m_objects.Remove(obj.Id))
        {
            Debug.LogError("Object not registered!");
        }

        obj.Id = 0;
    }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(m_objects.Count);

        foreach (KeyValuePair<int, ClientObject> pair in m_objects)
        {
            writer.Write(pair.Key);
            writer.Write(pair.Value.ObjectType);
            pair.Value.Serialize(writer);
        }
    }
}
