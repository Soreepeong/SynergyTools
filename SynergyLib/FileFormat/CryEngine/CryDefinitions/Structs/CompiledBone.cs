﻿using System;
using System.Text;
using SynergyLib.Util;
using SynergyLib.Util.BinaryRW;
using SynergyLib.Util.MathExtras;

namespace SynergyLib.FileFormat.CryEngine.CryDefinitions.Structs;

public class CompiledBone : ICryReadWrite {
    public uint ControllerId;
    public CompiledBonePhysics PhysicsLive;
    public CompiledBonePhysics PhysicsDead;
    public float Mass; // 0xD8 ?
    public Matrix3x4 LocalTransformMatrix; // Bind Pose Matrix
    public Matrix3x4 WorldTransformMatrix;
    public string Name = string.Empty;
    public uint LimbId; // ID of this limb... usually just 0xFFFFFFFF
    public int ParentOffset; // offset to the parent in number of CompiledBone structs (584 bytes)
    public int ChildOffset; // Offset to the first child to this bone in number of CompiledBone structs
    public int ChildCount; // Number of children to this bone

    public uint ControllerIdComputed => Crc32.CryE.Get(Name);

    public void ReadFrom(NativeReader reader, int expectedSize) {
        if (expectedSize == 584) {
            reader.ReadInto(out ControllerId);
            PhysicsLive.ReadFrom(reader, 104); // LOD 0 is the physics of alive body, 
            PhysicsDead.ReadFrom(reader, 104); // LOD 1 is the physics of a dead body
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
        using (writer.ScopedBigEndian(useBigEndian)) {
            writer.Write(ControllerId);
            PhysicsLive.WriteTo(writer, useBigEndian);
            PhysicsDead.WriteTo(writer, useBigEndian);
            writer.Write(Mass);
            writer.Write(LocalTransformMatrix);
            writer.Write(WorldTransformMatrix);
            writer.WriteFString(Name, 256, Encoding.UTF8);
            writer.Write(LimbId);
            writer.Write(ParentOffset);
            writer.Write(ChildCount);
            writer.Write(ChildOffset);
        }
    }

    public int WrittenSize => 584;

    public override string ToString() => $"{nameof(CompiledBone)} {ControllerId:X08} \"{Name}\"";
}
