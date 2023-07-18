﻿using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SynergyLib.FileFormat;
using SynergyLib.FileFormat.CryEngine;
using SynergyLib.FileFormat.GltfInterop;
using SynergyLib.Util;
using SynergyLib.Util.MathExtras;

namespace SynergyTools.ProgramCommands;

public class TestDevProgramCommand : RootProgramCommand {
    public new static readonly Command Command = new("testdev");

    public static readonly Argument<string[]> PathArgument = new("path") {
        Arity = ArgumentArity.OneOrMore,
    };

    static TestDevProgramCommand() {
        Command.AddArgument(PathArgument);
        Command.SetHandler(ic => new TestDevProgramCommand(ic.ParseResult).Handle(ic.GetCancellationToken()));
    }

    public readonly string[] InPathArray;

    public TestDevProgramCommand(ParseResult parseResult) : base(parseResult) {
        InPathArray = parseResult.GetValueForArgument(PathArgument);
    }

    public async Task<int> Handle(CancellationToken cancellationToken) {
        const string testLevelName = "hub02_seasidevillage";

        var reader = new GameFileSystemReader();
        foreach (var p in InPathArray)
            reader.WithRootDirectory(p);
        
        var rfn = reader.AsFunc(SkinFlag.LookupDefault);
        var sonic = await CryCharacter.FromCryEngineFiles(
            rfn,
            "objects/characters/1_heroes/sonic/sonic",
            cancellationToken);
        // var shadow = CryCharacter.FromCryEngineFiles(rfn, "objects/characters/5_minibosses/shadow/shadow");
        // var metal = CryCharacter.FromCryEngineFiles(rfn, "objects/characters/5_minibosses/metal_sonic/metal_sonic");

        var aabbSonic = AaBb.NegativeExtreme;
        foreach (var m in sonic.Model.Meshes)
        foreach (var p in m.Vertices)
            aabbSonic.Expand(p.Position);

        await using (var os = File.Create("Z:/ROL3D/sonic.glb"))
            (await sonic.ToGltf(rfn, true, false, cancellationToken)).Compile(os);
        // var char2 = CryCharacter.FromGltf(GltfTuple.FromStream(File.OpenRead("Z:/ROL3D/sonic.glb")));
        // foreach (var k in sonic.CryAnimationDatabase.Animations.Keys.ToArray()) {
        //     // var orig = sonic.CryAnimationDatabase.Animations[k];
        //     var recr = char2.CryAnimationDatabase.Animations.Single(x => k.EndsWith($"/{x.Key}.caf")).Value;
        //     // Debugger.Break();
        //     sonic.CryAnimationDatabase.Animations[k] = recr;
        // }

        var char2 = CryCharacter.FromGltf(GltfTuple.FromStream(File.OpenRead("Z:/ROL3D/m0361_b0001_v0001.glb")));
        var aabbM2 = AaBb.NegativeExtreme;
        foreach (var m in char2.Model.Meshes)
        foreach (var p in m.Vertices)
            aabbM2.Expand(p.Position);

        char2.Scale(2 * aabbSonic.Radius / aabbM2.Radius);

        var level = await reader.GetPackfile(testLevelName);
        foreach (var (k, v) in char2.Model.ExtraTextures)
            level.PutEntry(0, k, new(v.ToArray()));
        level.GetEntry(sonic.Definition!.Model!.File!, false).Source = new(char2.Model.GetGeometryBytes());
        level.GetEntry(sonic.Definition!.Model!.Material!, false).Source =
            new(char2.Model.GetMaterialBytes());

        sonic.CryAnimationDatabase!.Animations["animations/characters/1_heroes/sonic/final/combat_idle.caf"] =
            sonic.CryAnimationDatabase.Animations["animations/characters/1_heroes/sonic/final/idle.caf"] =
                char2.CryAnimationDatabase!.Animations[
                    "chara/monster/m0361/animation/a0001/bt_common/resident/monster.pap/cbbm_id0"];
        level.GetEntry(sonic.CharacterParameters!.TracksDatabasePath!, false).Source =
            new(sonic.CryAnimationDatabase!.GetBytes());
        var targetPath = reader.GetPackfilePath(testLevelName);
        while (targetPath.EndsWith(".bak"))
            targetPath = targetPath[..^4];
        await CompressProgramCommand.WriteAndPrintProgress(
            targetPath,
            level,
            default,
            cancellationToken);
        return 0;
    }
}
