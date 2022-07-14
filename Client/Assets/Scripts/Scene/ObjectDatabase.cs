using System.Collections.Generic;
using UnityEngine;

public class ObjectDatabase : MonoSingleton<ObjectDatabase>, IObjectDatabase
{
    [SerializeField]
    ObjectStorage m_storage;

    readonly Dictionary<int, Object> m_database = new Dictionary<int, Object>();

    protected override void Awake()
    {
        base.Awake();
        foreach (ObjectStorage.Entry entry in m_storage.Entries)
        {
            m_database.Add(entry.Id, entry.Object);
        }
    }

    public TObj GetObject<TObj>(int id) where TObj : Object
    {
        return m_database.GetValueOrDefault(id) as TObj;
    }
}
