using System;

namespace SynergyLib.FileFormat.CryEngine.CryDefinitions.Enums;

[Flags]
public enum MeshChunkFlags {
    MeshIsEmpty = 0x0001,
    HasTexMappingDensity = 0x0002,
}