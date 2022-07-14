using System;
using UnityEngine;
using FFmpeg.AutoGen;
using System.Runtime.InteropServices;

public unsafe class FrameConverter : IDisposable
{
    readonly IntPtr m_convertedFrameBufferPtr;
    readonly Vector2Int m_destinationSize;
    readonly byte_ptrArray4 m_dstData;
    readonly int_array4 m_dstLinesize;
    readonly SwsContext* m_pConvertContext;

    public FrameConverter(Vector2Int sourceSize, AVPixelFormat sourcePixelFormat,
        Vector2Int destinationSize, AVPixelFormat destinationPixelFormat)
    {
        m_destinationSize = destinationSize;

        m_pConvertContext = ffmpeg.sws_getContext(sourceSize.x,
            sourceSize.y,
            sourcePixelFormat,
            destinationSize.x,
            destinationSize.y,
            destinationPixelFormat,
            ffmpeg.SWS_FAST_BILINEAR,
            null,
            null,
            null);
        if (m_pConvertContext == null)
            throw new ApplicationException("Could not initialize the conversion context.");

        int convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(destinationPixelFormat,
            destinationSize.x,
            destinationSize.y,
            1);
        m_convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
        m_dstData = new byte_ptrArray4();
        m_dstLinesize = new int_array4();

        ffmpeg.av_image_fill_arrays(ref m_dstData,
            ref m_dstLinesize,
            (byte*)m_convertedFrameBufferPtr,
            destinationPixelFormat,
            destinationSize.x,
            destinationSize.y,
            1);
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(m_convertedFrameBufferPtr);
        ffmpeg.sws_freeContext(m_pConvertContext);
    }

    public AVFrame Convert(AVFrame sourceFrame)
    {
        ffmpeg.sws_scale(m_pConvertContext,
            sourceFrame.data,
            sourceFrame.linesize,
            0,
            sourceFrame.height,
            m_dstData,
            m_dstLinesize);

        byte_ptrArray8 data = new byte_ptrArray8();
        data.UpdateFrom(m_dstData);
        int_array8 linesize = new int_array8();
        linesize.UpdateFrom(m_dstLinesize);

        return new AVFrame
        {
            data = data,
            linesize = linesize,
            width = m_destinationSize.x,
            height = m_destinationSize.y
        };
    }
}
