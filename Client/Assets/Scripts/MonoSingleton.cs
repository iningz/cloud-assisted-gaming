using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    static T m_instance = null;

    public static T Instance => m_instance;

    protected virtual void Awake()
    {
        if (m_instance == null)
        {
            m_instance = (T)this;
        }
        else if (m_instance != this)
        {
            Destroy(this);
            Debug.LogError($"Singleton {name} of type {m_instance.GetType()} is duplicated!");
        }
    }

    protected virtual void OnDestroy()
    {
        if (m_instance == this)
        {
            m_instance = null;
        }
    }

    public static bool TryExecute(System.Action<T> action)
    {
        if (m_instance != null)
        {
            action?.Invoke(m_instance);
            return true;
        }
        else
        {
            return false;
        }
    }
}
