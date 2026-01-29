using System;
using System.Collections.Generic;

public sealed class StxEtxFramer
{
    public const byte STX = 0x02;
    public const byte ETX = 0x03;

    private readonly List<byte> _buf = new();
    private bool _inFrame;

    public int MaxFrameBytes { get; }

    public StxEtxFramer(int maxFrameBytes = 8192)
    {
        MaxFrameBytes = maxFrameBytes;
    }

    // 들어온 바이트를 먹이고, 완성된 "Body"들을 반환
    public List<byte[]> Feed(ReadOnlySpan<byte> data, out string? warning)
    {
        warning = null;
        var frames = new List<byte[]>();

        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];

            if (!_inFrame)
            {
                // 프레임 밖: STX를 기다림
                if (b == STX)
                {
                    _inFrame = true;
                    _buf.Clear();
                }
                // STX 전의 바이트는 잡음이라 무시
                continue;
            }

            // 프레임 안
            if (b == ETX)
            {
                frames.Add(_buf.ToArray()); // Body 완성
                _buf.Clear();
                _inFrame = false;
                continue;
            }

            _buf.Add(b);

            if (_buf.Count > MaxFrameBytes)
            {
                warning = $"Frame too long (> {MaxFrameBytes}). Reset.";
                _buf.Clear();
                _inFrame = false;
            }
        }

        return frames;
    }

    public void Reset()
    {
        _buf.Clear();
        _inFrame = false;
    }
}
