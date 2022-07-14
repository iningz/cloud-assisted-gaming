using System.IO;
using UnityEngine;
using FFmpeg.AutoGen;
using System.Runtime.InteropServices;
using System;

public static class FFmpegHelper
{
    const string PATH = "Plugins/FFmpeg/";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void RegisterFFmpegBinaries()
    {
#if UNITY_EDITOR
        string path = Path.Combine(Application.dataPath, PATH);
        Debug.Log($"Setting FFmpeg bin path to: {path}");
        ffmpeg.RootPath = path;
#else
        throw new NotImplementedException();
#endif
    }

    public static unsafe string av_strerror(int error)
    {
        var bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
        var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
        return message;
    }

    public static int ThrowExceptionIfError(this int error)
    {
        if (error < 0) throw new ApplicationException(av_strerror(error));
        return error;

    }
}
