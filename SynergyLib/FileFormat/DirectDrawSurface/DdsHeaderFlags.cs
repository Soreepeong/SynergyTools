﻿using System;

namespace SynergyLib.FileFormat.DirectDrawSurface;

[Flags]
public enum DdsHeaderFlags {
    /// <summary>Required in every .dds file.</summary>
    Caps = 0x1,

    /// <summary>Required in every .dds file.</summary> 
    Height = 0x2,

    /// <summary>Required in every .dds file.</summary>
    Width = 0x4,

    /// <summary>Required when pitch is provided for an uncompressed texture.</summary> 
    Pitch = 0x8,

    /// <summary>Required in every .dds file.</summary> 
    PixelFormat = 0x1000,

    /// <summary>Required in a mipmapped texture.</summary> 
    MipmapCount = 0x20000,

    /// <summary>Required when pitch is provided for a compressed texture.</summary>
    LinearSize = 0x80000,

    /// <summary>Required in a depth texture</summary> 
    Depth = 0x800000,
    
    Dimension1 = Width,
    Dimension2 = Width | Height,
    Dimension3 = Width | Height | Depth,
    DimensionMask = Width | Height | Depth,
}