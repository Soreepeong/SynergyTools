using System.Numerics;
using SynergyLib.Util.MathExtras;

namespace SynergyLib.FileFormat.CryEngine.CryDefinitions.Structs;

public struct MeshTangent {
    public Vector4<short> TangentRaw;
    public Vector4<short> BinormalRaw;

    public Vector4 Tangent {
        get => new(
            1f * TangentRaw.X / short.MaxValue,
            1f * TangentRaw.Y / short.MaxValue,
            1f * TangentRaw.Z / short.MaxValue,
            1f * TangentRaw.W / short.MaxValue);
        set => TangentRaw = new(
            (short) (value.X * short.MaxValue),
            (short) (value.Y * short.MaxValue),
            (short) (value.Z * short.MaxValue),
            (short) (value.W * short.MaxValue));
    }

    public Vector4 Binormal {
        get => new(
            1f * BinormalRaw.X / short.MaxValue,
            1f * BinormalRaw.Y / short.MaxValue,
            1f * BinormalRaw.Z / short.MaxValue,
            1f * BinormalRaw.W / short.MaxValue);
        set => BinormalRaw = new(
            (short) (value.X * short.MaxValue),
            (short) (value.Y * short.MaxValue),
            (short) (value.Z * short.MaxValue),
            (short) (value.W * short.MaxValue));
    }

    public static MeshTangent FromNormalAndTangent(Vector3 normal, Vector4 tangent) =>
        new() {
            Tangent = tangent,
            Binormal = new(Vector3.Cross(tangent.DropW(), normal), tangent.W),
            // ^ TODO: might have to use normal/uv maps
        };
}
