using UnityEngine;

[CreateAssetMenu(menuName = "Mesh Config")]
public class MeshConfig : ScriptableObject
{
    [SerializeField]
    MeshFilter m_meshFilter;

    [SerializeField]
    MeshRenderer m_meshRenderer;

    public Mesh Mesh => m_meshFilter.sharedMesh;
    public Material[] Materials => m_meshRenderer.sharedMaterials;
}
