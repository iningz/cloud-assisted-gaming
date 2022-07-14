using System.IO;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class ClientLight : ClientObject
{
    Light m_light;

    public override byte ObjectType => 1;

    protected override void GetDependencies()
    {
        m_light = GetComponent<Light>();
    }

    public override void Serialize(BinaryWriter writer)
    {
        SerializeUtilities.SerializeTranform(writer, transform);
        SerializeUtilities.SerializeColor24(writer, m_light.color);
        writer.Write(m_light.intensity);
        writer.Write((byte)m_light.type);
        switch (m_light.type)
        {
            case LightType.Spot:
                writer.Write(m_light.range);
                writer.Write(m_light.innerSpotAngle);
                writer.Write(m_light.spotAngle);
                break;
            case LightType.Directional:
                writer.Write(m_light.bounceIntensity);
                break;
            case LightType.Point:
                writer.Write(m_light.range);
                break;
            default:
                break;
        }
    }

    public override void Apply(IObjectDatabase database)
    {

    }
}
