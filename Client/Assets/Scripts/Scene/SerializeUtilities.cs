using System.IO;
using UnityEngine;

public static class SerializeUtilities
{
    public static void SerializeTranform(BinaryWriter writer, Transform transform)
    {
        Vector3 pos = transform.position;
        writer.Write(pos.x);
        writer.Write(pos.y);
        writer.Write(pos.z);

        Vector3 rot = transform.rotation.eulerAngles;
        writer.Write(rot.x);
        writer.Write(rot.y);
        writer.Write(rot.z);
    }

    public static void SerializeColor24(BinaryWriter writer, Color color)
    {
        writer.Write((byte)(color.r * 255));
        writer.Write((byte)(color.g * 255));
        writer.Write((byte)(color.b * 255));
    }
}
