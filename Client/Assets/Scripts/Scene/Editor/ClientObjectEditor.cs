using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ClientObject), true)]
public class ClientObjectEditor : Editor, IObjectDatabase
{
    ObjectStorage m_objectStorage = null;

    public TObj GetObject<TObj>(int id) where TObj : Object
    {
        if (m_objectStorage == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(ObjectStorage).Name);
            if (guids.Length == 0)
            {
                Debug.LogWarning("No object storage found!");
                return null;
            }

            if (guids.Length > 1)
            {
                Debug.LogWarning("More than one object storage exist!");
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            m_objectStorage = AssetDatabase.LoadAssetAtPath<ObjectStorage>(path);
        }

        foreach (ObjectStorage.Entry entry in m_objectStorage.Entries)
        {
            if (entry.Id == id)
            {
                return entry.Object as TObj;
            }
        }

        return null;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Apply"))
        {
            (target as ClientObject).GetDependenciesAndApply(this);
        }
    }
}
