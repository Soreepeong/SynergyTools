﻿using System;
using System.Text;
using WiiUStreamTool.Util.BinaryRW;
using WiiUStreamTool.Util.MathExtras;

namespace WiiUStreamTool.FileFormat.CryEngine.CryDefinitions.Structs;

public struct CompiledBone : ICryReadWrite {
    public uint ControllerId;
    public PhysicsGeometry[] PhysicsGeometry; // 2 of these. One for live objects, other for dead (ragdoll?)
    public double Mass; // 0xD8 ?
    public Matrix3x4 LocalTransformMatrix; // Bind Pose Matrix
    public Matrix3x4 WorldTransformMatrix;
    public string Name;
    public uint LimbId; // ID of this limb... usually just 0xFFFFFFFF
    public int ParentOffset; // offset to the parent in number of CompiledBone structs (584 bytes)
    public int ChildOffset; // Offset to the first child to this bone in number of CompiledBone structs
    public int ChildCount; // Number of children to this bone

    public void ReadFrom(NativeReader reader, int expectedSize) {
        if (expectedSize == 584) {
            reader.ReadInto(out ControllerId);
            PhysicsGeometry = new PhysicsGeometry[2];
            PhysicsGeometry[0].ReadFrom(reader, 104); // LOD 0 is the physics of alive body, 
            PhysicsGeometry[1].ReadFrom(reader, 104); // LOD 1 is the physics of a dead body
            Mass = reader.ReadSingle();
            LocalTransformMatrix = reader.ReadMatrix3x4();
            WorldTransformMatrix = reader.ReadMatrix3x4();
            Name = reader.ReadFString(256, Encoding.UTF8);
            reader.ReadInto(out LimbId);
            reader.ReadInto(out ParentOffset);
            reader.ReadInto(out ChildCount);
            reader.ReadInto(out ChildOffset);
        } else
            throw new NotSupportedException();
    }

    public void WriteTo(NativeWriter writer, bool useBigEndian) {
        throw new System.NotImplementedException();
    }

    public override string ToString() => $"{nameof(CompiledBone)} {ControllerId:X08} \"{Name}\"";
}