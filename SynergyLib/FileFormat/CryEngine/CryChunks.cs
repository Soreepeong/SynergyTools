﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using SynergyLib.FileFormat.CryEngine.CryDefinitions.Chunks;
using SynergyLib.FileFormat.CryEngine.CryDefinitions.Enums;
using SynergyLib.Util.BinaryRW;

namespace SynergyLib.FileFormat.CryEngine;

public class CryChunks : Dictionary<int, ICryChunk> {
    public static readonly ImmutableArray<byte> Magic = "CryTek\0"u8.ToArray().ToImmutableArray();

    public CryFileType Type;
    public CryFileVersion Version;

    public T AddChunkLE<T>(ChunkType type, int version, T chunk) where T : ICryChunk {
        chunk.Header = new() {
            Id = Count == 0 ? 1 : Values.Max(x => x.Header.Id) + 1,
            Type = type,
            VersionRaw = (uint) version,
        };
        Add(chunk.Header.Id, chunk);
        return chunk;
    }

    public T AddChunkBE<T>(ChunkType type, int version, T chunk) where T : ICryChunk {
        chunk.Header = new() {
            Id = Count == 0 ? 1 : Values.Max(x => x.Header.Id) + 1,
            Type = type,
            VersionRaw = (uint) version | 0x80000000u,
        };
        Add(chunk.Header.Id, chunk);
        return chunk;
    }

    public void ReadFrom(NativeReader reader) {
        using (reader.ScopedLittleEndian()) {
            reader.EnsureMagicOrThrow(Magic.AsSpan());
            reader.ReadByte();
            reader.ReadInto(out Type);
            if (!Enum.IsDefined(Type))
                throw new IOException("Bad FileType");
            reader.ReadInto(out Version);
            if (!Enum.IsDefined(Version))
                throw new IOException("Bad FileVersion");
            reader.ReadInto(out int chunkOffset);
            reader.ReadInto(out int chunkCount);
            reader.EnsurePositionOrThrow(chunkOffset + 4);

            var headers = new ChunkSizeChunk[chunkCount];
            for (var i = 0; i < chunkCount; i++) {
                headers[i] = new();
                headers[i].ReadFrom(reader, headers[i].WrittenSize);
            }

            for (var i = 0; i < chunkCount; i++) {
                ICryChunk chunk = (headers[i].Header.Type, headers[i].Header.Version) switch {
                    // chr, in order
                    (ChunkType.SourceInfo, 0) => new SourceInfoChunk {Header = headers[i].Header},
                    (ChunkType.Timing, 0x918) => new TimingChunk(),
                    (ChunkType.MtlName, 0x800) => new MtlNameChunk(),
                    (ChunkType.CompiledBones, 0x800) => new CompiledBonesChunk(),
                    (ChunkType.CompiledPhysicalBones, 0x800) => new CompiledPhysicalBonesChunk(),
                    (ChunkType.CompiledPhysicalProxies, 0x800) => new CompiledPhysicalProxyChunk(),
                    (ChunkType.CompiledMorphTargets, 0x800) => new CompiledMorphTargetsChunk(),
                    (ChunkType.CompiledIntSkinVertices, 0x800) => new CompiledIntSkinVerticesChunk(),
                    (ChunkType.CompiledIntFaces, 0x800) => new CompiledIntFacesChunk(),
                    (ChunkType.CompiledExt2IntMap, 0x800) => new CompiledExtToIntMapChunk(),
                    (ChunkType.BonesBoxes, 0x801) => new BonesBoxesChunk(),
                    (ChunkType.ExportFlags, 1) => new ExportFlagsChunk(),
                    (ChunkType.MeshPhysicsData, 0x800) => new MeshPhysicsDataChunk(),
                    (ChunkType.MeshSubsets, 0x800) => new MeshSubsetsChunk(),
                    (ChunkType.DataStream, 0x800) => new DataChunk(),
                    (ChunkType.Mesh, 0x800) => new MeshChunk(),
                    (ChunkType.Helper, 0x744) => new HelperChunk(),
                    (ChunkType.Node, 0x823) => new NodeChunk(),
                    (ChunkType.FoliageInfo, 1) => new FoliageInfoChunk(),

                    // dba, in order
                    (ChunkType.Controller, 0x905) => new ControllerChunk(),

                    _ => throw new NotSupportedException(headers[i].ToString()),
                };
                reader.BaseStream.Position = headers[i].Header.Offset;
                chunk.ReadFrom(reader, headers[i].Size);
                Add(headers[i].Header.Id, chunk);
            }

            for (var i = 0; i < chunkCount; i++)
                if (headers[i].Size != this[headers[i].Header.Id].WrittenSize)
                    throw new InvalidDataException();
        }

        if (reader.BaseStream.CanSeek)
            reader.EnsurePositionOrThrow(reader.BaseStream.Length);
    }

    public void WriteTo(NativeWriter writer) {
        writer.Write(Magic.AsSpan());
        writer.Write((byte) 0);
        writer.WriteEnum(Type);
        writer.WriteEnum(Version);
        writer.Write(20);
        writer.Write(Count);

        var chunks = this.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
        var chunkSizeChunks = new ChunkSizeChunk[Count];
        for (var i = 0; i < Count; i++) {
            chunkSizeChunks[i] = new() {Size = chunks[i].WrittenSize};
            (chunkSizeChunks[i].Header = chunks[i].Header).Offset = i == 0
                ? 24 + Count * chunkSizeChunks[i].WrittenSize
                : chunkSizeChunks[i - 1].Header.Offset + (chunkSizeChunks[i - 1].Size + 3) / 4 * 4;
        }

        foreach (var c in chunkSizeChunks)
            c.WriteTo(writer);

        foreach (var c in chunks) {
            writer.WritePadding(4);
            if (c.Header.Offset != writer.BaseStream.Position)
                throw new InvalidDataException();
            c.WriteTo(writer);
        }
    }

    public static CryChunks FromFile(string path) => FromBytes(File.ReadAllBytes(path));

    public static CryChunks FromBytes(byte[] inBytes) => FromStream(new MemoryStream(inBytes, false));

    public static CryChunks FromStream(Stream stream, bool leaveOpen = false) {
        try {
            var inBytes = new byte[stream.Length];
            stream.ReadExactly(inBytes);
            stream.Position = 0;

            var testfile = new CryChunks();
            using (var f = new NativeReader(stream, Encoding.UTF8, true))
                testfile.ReadFrom(f);

            byte[] outBytes;
            using (var ms = new MemoryStream())
            using (var f = new NativeWriter(ms)) {
                testfile.WriteTo(f);
                outBytes = ms.ToArray();
            }

            var ignoreZones = new List<Tuple<int, int>> {
                // random pad byte after CryTek\0
                new(7, 8),
            };
            // seems that if boneId array is not full, garbage values remain in place of unused memory.
            // ignore that from comparison.
            foreach (var ignoreItem in testfile.Values.OfType<MeshSubsetsChunk>())
                ignoreZones.Add(new(ignoreItem.Header.Offset, ignoreItem.Header.Offset + ignoreItem.WrittenSize));
            
            // size is set wrong for this chunk?
            foreach (var ignoreItem in testfile.Values.OfType<SourceInfoChunk>())
                ignoreZones.Add(new(ignoreItem.Header.Offset + 8, ignoreItem.Header.Offset + 12));

            ignoreZones.Add(Tuple.Create(31, 32));
            ignoreZones.Add(Tuple.Create(51, 52));

            for (int i = 0, to = Math.Min(inBytes.Length, outBytes.Length); i < to; i++) {
                if (inBytes[i] != outBytes[i] && !ignoreZones.Any(x => x.Item1 <= i && i < x.Item2))
                    throw new InvalidDataException();
            }

            if (inBytes.Length != outBytes.Length)
                throw new InvalidDataException();

            return testfile;
        } finally {
            if (!leaveOpen)
                stream.Dispose();
        }
    }
}
