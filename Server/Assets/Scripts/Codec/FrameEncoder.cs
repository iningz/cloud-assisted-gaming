using System;
using UnityEngine;
using FFmpeg.AutoGen;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public enum EncodePreset
{
    UltraFast,
    SuperFast,
    VeryFast,
    Faster,
    Fast,
    Medium,
    Slow,
    Slower,
    VerySlow,
    Placebo
}

public unsafe class FrameEncoder : IDisposable
{
    readonly Vector2Int m_frameSize;
    readonly int m_linesizeU;
    readonly int m_linesizeV;
    readonly int m_linesizeY;
    readonly AVCodec* m_pCodec;
    readonly AVCodecContext* m_pCodecContext;
    readonly int m_uSize;
    readonly int m_ySize;

    readonly FrameConverter m_converter;

    public FrameEncoder(int frameRate, Vector2Int frameSize, EncodePreset preset, int crf, int gop)
    {
        m_frameSize = frameSize;

        var codecId = AVCodecID.AV_CODEC_ID_H264;
        m_pCodec = ffmpeg.avcodec_find_encoder(codecId);
        if (m_pCodec == null) throw new InvalidOperationException("Codec not found.");

        m_pCodecContext = ffmpeg.avcodec_alloc_context3(m_pCodec);
        m_pCodecContext->gop_size = gop;
        m_pCodecContext->time_base = new AVRational { num = 1, den = frameRate };
        m_pCodecContext->width = frameSize.x;
        m_pCodecContext->height = frameSize.y;
        m_pCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
        ffmpeg.av_opt_set_int(m_pCodecContext->priv_data, "crf", crf, 0);
        ffmpeg.av_opt_set(m_pCodecContext->priv_data, "preset", Enum.GetName(typeof(EncodePreset), preset).ToLower(), 0);
        ffmpeg.av_opt_set(m_pCodecContext->priv_data, "tune", "zerolatency", 0);

        ffmpeg.avcodec_open2(m_pCodecContext, m_pCodec, null).ThrowExceptionIfError();

        m_linesizeY = frameSize.x;
        m_linesizeU = frameSize.x / 2;
        m_linesizeV = frameSize.x / 2;

        m_ySize = m_linesizeY * frameSize.y;
        m_uSize = m_linesizeU * frameSize.y / 2;

        m_converter = new FrameConverter(frameSize, AVPixelFormat.AV_PIX_FMT_RGBA, frameSize, AVPixelFormat.AV_PIX_FMT_YUV420P);
    }

    public void Dispose()
    {
        m_converter.Dispose();

        ffmpeg.avcodec_close(m_pCodecContext);
        ffmpeg.av_free(m_pCodecContext);
    }

    public void SetCrf(int crf)
    {
        ffmpeg.av_opt_set_int(m_pCodecContext->priv_data, "crf", crf, 0);
    }

    public byte[] Encode(NativeArray<byte> textureData)
    {
        AVFrame convertedFrame;
        byte_ptrArray8 data = new byte_ptrArray8 { [0] = (byte*)textureData.GetUnsafeReadOnlyPtr() };
        int_array8 linesize = new int_array8 { [0] = textureData.Length / m_frameSize.y };
        AVFrame sourceFrame = new AVFrame
        {
            data = data,
            linesize = linesize,
            height = m_frameSize.y
        };
        convertedFrame = m_converter.Convert(sourceFrame);

        AVPacket* pPacket = ffmpeg.av_packet_alloc();
        byte[] ret = null;
        try
        {
            int error;

            ffmpeg.avcodec_send_frame(m_pCodecContext, &convertedFrame).ThrowExceptionIfError();
            error = ffmpeg.avcodec_receive_packet(m_pCodecContext, pPacket);
            if (error == ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                return null;
            }

            error.ThrowExceptionIfError();

            ret = new byte[pPacket->size];
            UnmanagedMemoryStream packetStream = new UnmanagedMemoryStream(pPacket->data, pPacket->size);
            packetStream.Read(ret);
        }
        finally
        {
            ffmpeg.av_packet_free(&pPacket);
        }
        //Debug.Log($"Packet size: {ret.Length} bytes");
        return ret;
    }
}
