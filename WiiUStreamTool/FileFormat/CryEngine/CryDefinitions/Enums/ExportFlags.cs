using System;

namespace WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Enums;

[Flags]
public enum ExportFlags {
    MergeAllNodes = 0x1,
    HaveAutoLods = 0x2,
    UseCustomNormals = 0x4,
}