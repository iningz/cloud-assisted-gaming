using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClientMesh : ClientObject
{
    [SerializeField]
    int m_configID;

    MeshFilter m_meshFilter;
    MeshRenderer m_meshRenderer;

    public int ConfigID { get => m_configID; set => m_configID = value; }

    public override byte ObjectType => 0;

    protected override void GetDependencies()
    {
        m_meshFilter = GetComponent<MeshFilter>();
        m_meshRenderer = GetComponent<MeshRenderer>();
    }

    public override void Serialize(BinaryWriter writer)
    {
        SerializeUtilities.SerializeTranform(writer, transform);
        writer.Write(m_configID);
    }

    public override void Apply(IObjectDatabase database)
    {
        MeshConfig config = database.GetObject<MeshConfig>(m_configID);
        if (config == null)
        {
            Debug.LogWarning($"Cannot find Mesh Config with ID {m_configID}!");
            return;
        }
        m_meshFilter.mesh = config.Mesh;
        m_meshRenderer.sharedMaterials = config.Materials;
    }
}
