﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SynergyLib.FileFormat.CryEngine.CryXml.MaterialElements;

[JsonConverter(typeof(StringEnumConverter))]
public enum ETexModMoveType {
    NoChange,
    Fixed,
    Constant,
    Jitter,
    Pan,
    Stretch,
    StretchRepeat,
    Max
}
