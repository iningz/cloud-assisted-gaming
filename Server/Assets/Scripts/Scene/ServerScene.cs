using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ServerScene : MonoBehaviour
{
    [SerializeField]
    Camera m_camera;

    Dictionary<int, ServerObject> m_objects = new Dictionary<int, ServerObject>();

    public Camera Camera => m_camera;

    public void DeserializeScene(BinaryReader reader, ObjectDatabase database, ServerObjectGenerator generator)
    {
        Dictionary<int, ServerObject> nextObjects = new Dictionary<int, ServerObject>();

        try
        {
            DeserializeUtilities.DeserializeTransform(reader, m_camera.transform);
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                int type = reader.ReadByte();
                if (m_objects.TryGetValue(id, out ServerObject obj))
                {
                    m_objects.Remove(id);
                    nextObjects.Add(id, obj);

                    //should use type to check
                    obj.Deserialize(reader, database);
                }
                else
                {
                    obj = generator.Generate(type, transform);
                    nextObjects.Add(id, obj);

                    obj.Deserialize(reader, database);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Deserialize scene error: {ex}");
        }

        foreach (ServerObject obj in m_objects.Values)
        {
            Destroy(obj.gameObject);
        }

        m_objects = nextObjects;
    }
}
