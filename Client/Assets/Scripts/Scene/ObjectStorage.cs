using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Object Storage")]
public class ObjectStorage : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public int Id;
        public string Name;
        public Object Object;
    }

    [SerializeField]
    List<Entry> m_entires;

    public IReadOnlyList<Entry> Entries => m_entires;
}
