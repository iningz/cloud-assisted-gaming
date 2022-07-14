using System;
using UnityEngine;
using FFmpeg.AutoGen;
using System.IO;

public unsafe class FrameDecoder : IDisposable
{
    readonly int m_frameRate;
    readonly Vector2Int m_frameSize;
    readonly AVCodecContext* m_pCodecContext;
    readonly AVFrame* m_pFrame;
    readonly AVPacket* m_pPacket;

    readonly FrameConverter m_converter;

    public FrameDecoder(int frameRate, Vector2Int frameSize)
    {
        m_frameRate = frameRate;
        m_frameSize = frameSize;

        var codecId = AVCodecID.AV_CODEC_ID_H264;
        AVCodec* pCodec = ffmpeg.avcodec_find_decoder(codecId);
        if (pCodec == null) throw new InvalidOperationException("Codec not found.");

        m_pCodecContext = ffmpeg.avcodec_alloc_context3(pCodec);
        m_pCodecContext->width = frameSize.x;
        m_pCodecContext->height = frameSize.y;
        m_pCodecContext->time_base = new AVRational { num = 1, den = frameRate };
        m_pCodecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

        ffmpeg.avcodec_open2(m_pCodecContext, pCodec, null).ThrowExceptionIfError();

        m_pPacket = ffmpeg.av_packet_alloc();
        m_pFrame = ffmpeg.av_frame_alloc();

        m_converter = new FrameConverter(frameSize, AVPixelFormat.AV_PIX_FMT_YUV420P, frameSize, AVPixelFormat.AV_PIX_FMT_RGB24);
    }

    public void Dispose()
    {
        m_converter.Dispose();

        AVFrame* pFrame = m_pFrame;
        ffmpeg.av_frame_free(&pFrame);

        AVPacket* pPacket = m_pPacket;
        ffmpeg.av_packet_free(&pPacket);

        ffmpeg.avcodec_close(m_pCodecContext);
        ffmpeg.av_free(m_pCodecContext);
    }

    public bool Decode(ReadOnlySpan<byte> data, byte[] output)
    {
        ffmpeg.av_frame_unref(m_pFrame);
        int error;

        try
        {
            byte* pData = (byte*)ffmpeg.av_malloc((ulong)data.Length + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE);
            UnmanagedMemoryStream dataStream = new UnmanagedMemoryStream(pData, data.Length, data.Length, FileAccess.Write);
            dataStream.Write(data);

            ffmpeg.av_packet_from_data(m_pPacket, pData, data.Length);
            m_pPacket->dts = 0;
            m_pPacket->dts = 0;

            ffmpeg.avcodec_send_packet(m_pCodecContext, m_pPacket);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(ex.ToString());
        }
        finally
        {
            ffmpeg.av_packet_free_side_data(m_pPacket);
            ffmpeg.av_packet_unref(m_pPacket);
        }

        error = ffmpeg.avcodec_receive_frame(m_pCodecContext, m_pFrame);

        if (error == ffmpeg.AVERROR(ffmpeg.EAGAIN))
        {
            return false;
        }

        error.ThrowExceptionIfError();

        AVFrame convertedFrame = m_converter.Convert(*m_pFrame);

        int length = convertedFrame.height * convertedFrame.linesize[0];
        UnmanagedMemoryStream frameStream = new UnmanagedMemoryStream(convertedFrame.data[0], output.LongLength);
        frameStream.Read(output);
        return true;
    }
}
