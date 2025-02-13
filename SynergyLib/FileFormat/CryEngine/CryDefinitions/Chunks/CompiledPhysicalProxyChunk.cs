﻿using System.Collections.Generic;
using System.Linq;
using SynergyLib.FileFormat.CryEngine.CryDefinitions.Structs;
using SynergyLib.Util.BinaryRW;

namespace SynergyLib.FileFormat.CryEngine.CryDefinitions.Chunks;

public class CompiledPhysicalProxyChunk : ICryChunk {
    public ChunkHeader Header { get; set; } = new();
    public readonly List<CompiledPhysicalProxy> Proxies = new();

    public CompiledPhysicalProxyChunk() { }

    public void ReadFrom(NativeReader reader, int expectedSize) {
        var expectedEnd = reader.BaseStream.Position + expectedSize;
        Header = new(reader);
        using (reader.ScopedBigEndian(Header.IsBigEndian)) {
            reader.ReadInto(out int count);
            Proxies.Clear();
            Proxies.EnsureCapacity(count);

            var proxy = new CompiledPhysicalProxy();
            for (var i = 0; i < count; i++) {
                proxy.ReadFrom(reader, -1);
                Proxies.Add(proxy);
            }
        }

        reader.EnsurePositionOrThrow(expectedEnd);
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        Header.WriteTo(writer, false);
        using (writer.ScopedBigEndian(useBigEndian)) {
            writer.Write(Proxies.Count);
            foreach (var proxy in Proxies)
                proxy.WriteTo(writer, useBigEndian);
        }
    }

    public int WrittenSize => Header.WrittenSize + 4 + Proxies.Sum(x => x.WrittenSize);

    public override string ToString() => $"{nameof(CompiledPhysicalProxyChunk)}: {Header}";
}
