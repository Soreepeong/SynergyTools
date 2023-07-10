﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Structs;
using WiiUStreamTool.Util.BinaryRW;

namespace WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Chunks;

public struct CompiledPhysicalBonesChunk : ICryReadWrite {
    public ChunkHeader Header;
    public readonly List<CompiledPhysicalBone> Bones = new();

    public CompiledPhysicalBonesChunk() { }

    public void ReadFrom(NativeReader reader, int expectedSize) {
        var expectedEnd = reader.BaseStream.Position + expectedSize;
        Header.ReadFrom(reader, Unsafe.SizeOf<ChunkHeader>());
        using (reader.ScopedBigEndian(Header.IsBigEndian)) {
            reader.EnsureZeroesOrThrow(32);

            var boneCount = (int) ((expectedEnd - reader.BaseStream.Position) / 152);
            Bones.EnsureCapacity(boneCount);
            Bones.Clear();
            var bone = new CompiledPhysicalBone();
            for (var i = 0; i < boneCount; i++) {
                bone.ReadFrom(reader, 152);
                boneCount = Math.Max(boneCount, i + bone.ChildCount);
                Bones.Add(bone);
            }
        }

        reader.EnsurePositionOrThrow(expectedEnd);
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        throw new NotImplementedException();
    }

    public override string ToString() => $"{nameof(CompiledPhysicalBonesChunk)}: {Header}";
}