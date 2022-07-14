using UnityEngine;

public static class Bootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Bootstrap()
    {
        Object.DontDestroyOnLoad(Object.Instantiate(Resources.Load("Systems")));
        Debug.Log("Bootstrap completed.");
    }
}
