﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using WiiUStreamTool.Util.BinaryRW;

namespace WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Chunks;

[StructLayout(LayoutKind.Explicit)]
public struct CompiledIntFace : ICryReadWrite {
    [FieldOffset(0)] public ushort Face0;
    [FieldOffset(2)] public ushort Face1;
    [FieldOffset(4)] public ushort Face2;
    [FieldOffset(0)] public unsafe fixed ushort Faces[3];

    public void ReadFrom(NativeReader reader, int expectedSize) {
        if (expectedSize != 6)
            throw new IOException();
        reader.ReadInto(out Face0);
        reader.ReadInto(out Face1);
        reader.ReadInto(out Face2);
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        throw new NotImplementedException();
    }
}