using System.IO;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class ServerLight : ServerObject
{
    Light m_light;

    void Awake()
    {
        m_light = GetComponent<Light>();
    }

    public override void Deserialize(BinaryReader reader, ObjectDatabase database)
    {
        DeserializeUtilities.DeserializeTransform(reader, transform);
        m_light.color = DeserializeUtilities.DeserializeColor24(reader);
        m_light.intensity = reader.ReadSingle();
        LightType type = (LightType)reader.ReadByte();
        m_light.type = type;
        switch (type)
        {
            case LightType.Spot:
                m_light.range = reader.ReadSingle();
                m_light.innerSpotAngle = reader.ReadSingle();
                m_light.spotAngle = reader.ReadSingle();
                break;
            case LightType.Directional:
                m_light.bounceIntensity = reader.ReadSingle();
                break;
            case LightType.Point:
                m_light.range = reader.ReadSingle();
                break;
            default:
                break;
        }
    }
}
