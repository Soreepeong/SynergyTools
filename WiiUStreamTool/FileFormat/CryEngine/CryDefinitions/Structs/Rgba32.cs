﻿using System.IO;
using System.Runtime.InteropServices;
using WiiUStreamTool.Util.BinaryRW;

namespace WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Chunks;

[StructLayout(LayoutKind.Explicit)]
public struct Rgba32 : ICryReadWrite {
    [FieldOffset(0)] public byte R;
    [FieldOffset(1)] public byte G;
    [FieldOffset(2)] public byte B;
    [FieldOffset(3)] public byte A;
    [FieldOffset(0)] public uint Value;
    
    public void ReadFrom(NativeReader reader, int expectedSize) {
        if (expectedSize != 4)
            throw new IOException();
        R = reader.ReadByte();
        G = reader.ReadByte();
        B = reader.ReadByte();
        A = reader.ReadByte();
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        writer.Write(R);
        writer.Write(G);
        writer.Write(B);
        writer.Write(A);
    }
}