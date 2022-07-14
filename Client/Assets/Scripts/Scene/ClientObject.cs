using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class ClientObject : MonoBehaviour
{
    public int Id { get; set; }

    public abstract byte ObjectType { get; }

    protected virtual void Awake()
    {
        ClientObjectManager.Instance.RegisterObject(this);
        GetDependencies();
    }

    protected virtual void OnDestroy()
    {
        ClientObjectManager.TryExecute(manager => manager.UnregisterObject(this));
    }

    protected abstract void GetDependencies();

    public abstract void Serialize(BinaryWriter writer);

    public abstract void Apply(IObjectDatabase database);

#if UNITY_EDITOR
    public void GetDependenciesAndApply(IObjectDatabase database)
    {
        GetDependencies();
        Apply(database);
    }
#endif
}
