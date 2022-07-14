using UnityEngine;

public interface IObjectDatabase
{
    TObj GetObject<TObj>(int id) where TObj : Object;
}
