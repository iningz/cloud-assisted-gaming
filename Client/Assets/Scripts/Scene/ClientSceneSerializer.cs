using System.IO;
using UnityEngine;

public class ClientSceneSerializer : MonoBehaviour
{
    [SerializeField]
    Camera m_camera;

    public bool SerializeScene(BinaryWriter writer)
    {
        try
        {
            SerializeUtilities.SerializeTranform(writer, m_camera.transform);
            ClientObjectManager.Instance.Serialize(writer);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
        
        return true;
    }
}
