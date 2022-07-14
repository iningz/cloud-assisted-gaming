using System.IO;
using UnityEngine;

public static class DeserializeUtilities
{
    public static void DeserializeTransform(BinaryReader reader, Transform transform)
    {
        transform.SetPositionAndRotation(
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            Quaternion.Euler(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
    }

    public static Color DeserializeColor24(BinaryReader reader)
    {
        return new Color(
            reader.ReadByte() / 255f,
            reader.ReadByte() / 255f,
            reader.ReadByte() / 255f);
    }
}
