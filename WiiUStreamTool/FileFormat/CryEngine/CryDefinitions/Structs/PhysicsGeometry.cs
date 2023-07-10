using System;
using System.Numerics;
using WiiUStreamTool.Util.BinaryRW;
using WiiUStreamTool.Util.MathExtras;

namespace WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Structs;

public struct PhysicsGeometry : ICryReadWrite {
    public uint PhysicsGeom;
    public uint Flags; // 0x0C ?
    public Vector3 Min;
    public Vector3 Max;
    public Vector3 SpringAngle;
    public Vector3 SpringTension;
    public Vector3 Damping;
    public Matrix3x3 Framemtx;

    public void ReadFrom(NativeReader reader, int expectedSize) {
        if (expectedSize == 104) {
            PhysicsGeom = reader.ReadUInt32();
            Flags = reader.ReadUInt32();
            Min = reader.ReadVector3();
            Max = reader.ReadVector3();
            SpringAngle = reader.ReadVector3();
            SpringTension = reader.ReadVector3();
            Damping = reader.ReadVector3();
            Framemtx = reader.ReadMatrix3x3();
        } else
            throw new NotSupportedException();
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        throw new System.NotImplementedException();
    }
}