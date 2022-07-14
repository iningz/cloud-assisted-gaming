using System.IO;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ServerMesh : ServerObject
{
    MeshFilter m_meshFilter;
    MeshRenderer m_meshRenderer;

    public int ConfigID { get; set; } = 0;

    void Awake()
    {
        m_meshFilter = GetComponent<MeshFilter>();
        m_meshRenderer = GetComponent<MeshRenderer>();
    }

    public override void Deserialize(BinaryReader reader, ObjectDatabase database)
    {
        DeserializeUtilities.DeserializeTransform(reader, transform);
        int configId = reader.ReadInt32();
        if (configId != ConfigID)
        {
            ConfigID = configId;
            MeshConfig config = database.GetObject<MeshConfig>(configId);
            m_meshFilter.mesh = config.Mesh;
            m_meshRenderer.sharedMaterials = config.Materials;
        }
    }
}
