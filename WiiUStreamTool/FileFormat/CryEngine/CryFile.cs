﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Chunks;
using WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Enums;
using WiiUStreamTool.Util.BinaryRW;

namespace WiiUStreamTool.FileFormat.CryEngine;

public class CryFile : Dictionary<int, ICryChunk> {
    public static readonly ImmutableArray<byte> Magic = "CryTek\0\0"u8.ToArray().ToImmutableArray();

    public CryFileType Type;
    public CryFileVersion Version;

    public void ReadFrom(NativeReader reader) {
        using (reader.ScopedLittleEndian()) {
            reader.EnsureMagicOrThrow(Magic.AsSpan());
            reader.ReadInto(out Type);
            if (!Enum.IsDefined(Type))
                throw new IOException("Bad FileType");
            reader.ReadInto(out Version);
            if (!Enum.IsDefined(Version))
                throw new IOException("Bad FileVersion");
            reader.ReadInto(out int chunkOffset);
            reader.ReadInto(out int chunkCount);
            reader.EnsurePositionOrThrow(chunkOffset + 4);

            Span<ChunkSizeChunk> headers = stackalloc ChunkSizeChunk[chunkCount];
            for (var i = 0; i < chunkCount; i++)
                headers[i].ReadFrom(reader, Unsafe.SizeOf<ChunkSizeChunk>());

            for (var i = 0; i < chunkCount; i++) {
                ICryChunk chunk = (headers[i].Header.Type, headers[i].Header.Version) switch {
                    // Geometry
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
                    (ChunkType.Node, 0x823) => new NodeChunk(),
                    
                    // Animation
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
        writer.WriteEnum(Type);
        writer.WriteEnum(Version);
        writer.Write(20);
        writer.Write(Count);

        var chunks = this.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
        var chunkSizeChunks = new ChunkSizeChunk[Count];
        for (var i = 0; i < Count; i++) {
            chunkSizeChunks[i].Size = chunks[i].WrittenSize;
            chunkSizeChunks[i].Header = chunks[i].Header with {
                Offset = i == 0
                    ? 24 + Count * chunkSizeChunks[i].WrittenSize
                    : chunkSizeChunks[i - 1].Header.Offset + (chunkSizeChunks[i - 1].Size + 3) / 4 * 4,
            };
        }

        foreach (var c in chunkSizeChunks)
            c.WriteTo(writer);

        foreach (var c in chunks) {
            if (c.Header.Offset != writer.BaseStream.Position)
                throw new InvalidDataException();
            c.WriteTo(writer);
            writer.WritePadding(4);
        }
    }
}