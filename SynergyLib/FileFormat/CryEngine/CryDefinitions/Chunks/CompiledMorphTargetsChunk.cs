﻿using System.Collections.Generic;
using System.Linq;
using SynergyLib.FileFormat.CryEngine.CryDefinitions.Structs;
using SynergyLib.Util.BinaryRW;

namespace SynergyLib.FileFormat.CryEngine.CryDefinitions.Chunks;

public class CompiledMorphTargetsChunk : ICryChunk {
    public ChunkHeader Header { get; set; } = new();
    public readonly List<CompiledMorphTarget> Targets = new();

    public CompiledMorphTargetsChunk() { }

    public void ReadFrom(NativeReader reader, int expectedSize) {
        var expectedEnd = reader.BaseStream.Position + expectedSize;
        Header = new(reader);
        using (reader.ScopedBigEndian(Header.IsBigEndian)) {
            reader.ReadInto(out int count);
            Targets.Clear();
            Targets.EnsureCapacity(count);

            var target = new CompiledMorphTarget();
            for (var i = 0; i < count; i++) {
                target.ReadFrom(reader, 16);
                Targets.Add(target);
            }
        }

        reader.EnsurePositionOrThrow(expectedEnd);
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        Header.WriteTo(writer, false);
        using (writer.ScopedBigEndian(useBigEndian)) {
            writer.Write(Targets.Count);
            foreach (var target in Targets)
                target.WriteTo(writer, useBigEndian);
        }
    }

    public int WrittenSize => Header.WrittenSize + 4 + Targets.Sum(x => x.WrittenSize);

    public override string ToString() => $"{nameof(CompiledMorphTargetsChunk)}: {Header}";
}
