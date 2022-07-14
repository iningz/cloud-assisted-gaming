using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class ServerObject : MonoBehaviour
{
    public int Id { get; set; }

    public abstract void Deserialize(BinaryReader reader, ObjectDatabase database);
}
