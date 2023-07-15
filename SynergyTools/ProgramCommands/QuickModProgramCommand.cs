﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SynergyLib.FileFormat;
using SynergyTools.Misc;

namespace SynergyTools.ProgramCommands;

public class QuickModProgramCommand : RootProgramCommand {
    public new static readonly Command Command = new("quickmod");

    public static readonly Argument<string[]> PathArgument = new(
        "path",
        "Specify root content directory, such as \"C:\\mlc01\\usr\\title\\00050000\\10175b00\\content\".\n" +
        "If you have an update applied, specify the update first, and then the base game next.") {
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly Option<SonicClones> ModeOption = new(
        "--mode",
        () => SonicClones.Default,
        "Specify which character to replace Sonic.");

    public static readonly Option<SonicCloneVoices> VoiceOption = new(
        "--voice",
        () => SonicCloneVoices.Auto,
        "Specify which voice to replace Sonic's voice.");

    private const string SonicBaseName = "objects/characters/1_heroes/sonic/sonic";

    private static readonly Tuple<string, int>[] DesaturationTargetTextures = {
        Tuple.Create("art/textures/effects/playerfx/ball_blue.dds", 18),
        Tuple.Create("art/textures/effects/playerfx/glide_sonic.dds", 3),
        Tuple.Create("art/textures/effects/playerfx/glide_sonic_soft.dds", 3),
        Tuple.Create("art/textures/effects/playerfx/bungee_sonic.dds", 3),
        Tuple.Create("art/textures/effects/playerfx/bungee_sonic_additive.dds", 3),
    };

    public static readonly Dictionary<string, string> SonicToShadowExertionMap = new() {
        ["Sonic_disgusted_17"] = "Shadow_disgusted",
        ["Sonic_disgusted_18"] = "Shadow_disgusted",
        ["Sonic_disgusted_19"] = "Shadow_disgusted",
        ["Sonic_disgusted_21"] = "Shadow_disgusted",
        ["Sonic_boost_pad"] = "",
        ["Sonic_boost_ring_gate"] = "",
        ["Sonic_boost_ring_gate_super"] = "",
        ["Sonic_bouncepad"] = "Shadow_sprintbegin",
        ["Sonic_bungee_pull_target_2D"] = "",
        ["Sonic_buttstomp_button"] = "",
        ["Sonic_buttstomp_button_and_breakable"] = "",
        ["Sonic_buttstomp_compliment"] = "",
        ["Sonic_charselect"] = "Shadow_positive",
        ["Sonic_collect_shiny"] = "Shadow_laugh",
        ["Sonic_combat_begin"] = "",
        ["Sonic_death_via_combat"] = "Shadow_frustrated",
        ["Sonic_death_via_drown"] = "Shadow_frustrated",
        ["Sonic_death_via_fall"] = "Shadow_frustrated",
        ["Sonic_death_via_hazard"] = "Shadow_frustrated",
        ["Sonic_death_via_road"] = "Shadow_frustrated",
        ["Sonic_death_via_tunnelbot"] = "Shadow_frustrated",
        ["Sonic_modal_weapon_pickup"] = "",
        ["Sonic_slingshotpull_noplay"] = "Shadow_bungeethrow_noplay",
        ["Sonic_spindash"] = "Shadow_dashbegin",
        ["Sonic_warthog_begin"] = "",
        ["Sonic_warthog_running"] = "",
    };

    static QuickModProgramCommand() {
        Command.AddAlias("qm");
        Command.AddAlias("metadow");
        Command.AddArgument(PathArgument);
        ModeOption.AddAlias("-m");
        Command.AddOption(ModeOption);
        VoiceOption.AddAlias("-v");
        Command.AddOption(VoiceOption);
        Command.SetHandler(ic => new QuickModProgramCommand(ic.ParseResult).Handle(ic.GetCancellationToken()));
    }

    public readonly string[] PathArray;
    public readonly SonicClones Mode;
    public readonly SonicCloneVoices Voice;
    
    public string? SoundsPath;
    public string? HeroesPath;
    public WiiuStreamFile? Heroes;
    public readonly Dictionary<string, WiiuStreamFile> Levels = new();

    public QuickModProgramCommand(ParseResult parseResult) : base(parseResult) {
        PathArray = parseResult.GetValueForArgument(PathArgument);
        Mode = parseResult.GetValueForOption(ModeOption);
        Voice = parseResult.GetValueForOption(VoiceOption);
    }

    private WiiuStreamFile ReferenceLevel => Levels[Mode switch {
        SonicClones.Shadow => "level02_ancientfactorypresent_a",
        SonicClones.MetalSonic => "level05_sunkenruins",
        SonicClones.Sonic => throw new InvalidOperationException(),
        _ => throw new InvalidOperationException(),
    }];

    private string ReferenceObjectBaseName => Mode switch {
        SonicClones.Shadow => "objects/characters/5_minibosses/shadow/shadow",
        SonicClones.MetalSonic => "objects/characters/5_minibosses/metal_sonic/metal_sonic",
        SonicClones.Sonic => throw new InvalidOperationException(),
        _ => throw new InvalidOperationException(),
    };

    private string ReferenceTexturePathPrefix => Mode switch {
        SonicClones.Shadow => "art/characters/5_minibosses/shadow/texture/",
        SonicClones.MetalSonic => "art/characters/5_minibosses/metal_sonic/textures/",
        SonicClones.Sonic => throw new InvalidOperationException(),
        _ => throw new InvalidOperationException(),
    };

    public async Task<int> Handle(CancellationToken cancellationToken) {
        Heroes = null;
        Levels.Clear();

        var levelPaths = new Dictionary<string, string>();

        Console.WriteLine("Looking for files to patch...");
        foreach (var inPath in PathArray) {
            var levelsPath = Path.Join(inPath, "Sonic_Crytek", "Levels");
            if (!Directory.Exists(levelsPath))
                throw new DirectoryNotFoundException("Given path does not contain Sonic_Crytek\\Level folder.");

            if (Directory.Exists(Path.Join(inPath, "Sonic_Crytek", "Sounds")))
                SoundsPath = Path.Join(inPath, "Sonic_Crytek", "Sounds");

            if (HeroesPath is null) {
                HeroesPath = Path.Join(inPath, "Sonic_Crytek", "heroes.wiiu.stream");
                var bakFile = HeroesPath + ".bak";
                if (Mode == SonicClones.Sonic) {
                    if (File.Exists(bakFile)) {
                        File.Delete(HeroesPath);
                        File.Move(bakFile, HeroesPath);
                        Console.WriteLine(
                            "Restored: {0} from {1}",
                            Path.GetFileName(HeroesPath),
                            Path.GetFileName(bakFile));
                    }
                } else if (File.Exists(HeroesPath)) {
                    if (!File.Exists(bakFile)) {
                        File.Copy(HeroesPath, bakFile);
                        Console.WriteLine("Made a backup copy: {0}", Path.GetFileName(bakFile));
                    }

                    Heroes = new();
                    Heroes.ReadFrom(null, bakFile, cancellationToken);
                } else {
                    HeroesPath = null;
                }
            }

            foreach (var levelPath in Directory.GetFiles(levelsPath)) {
                if (!levelPath.EndsWith(".wiiu.stream", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var bakFile = levelPath + ".bak";
                if (Mode == SonicClones.Sonic) {
                    if (File.Exists(bakFile)) {
                        File.Delete(levelPath);
                        File.Move(bakFile, levelPath);
                        Console.WriteLine(
                            "Restored: {0} from {1}",
                            Path.GetFileName(levelPath),
                            Path.GetFileName(bakFile));
                    }

                    continue;
                }

                var key = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(levelPath))
                    .ToLowerInvariant();
                if (Levels.ContainsKey(key))
                    continue;

                if (!File.Exists(bakFile)) {
                    File.Copy(levelPath, bakFile);
                    Console.WriteLine("Made a backup copy: {0}", Path.GetFileName(bakFile));
                }

                var strm = new WiiuStreamFile();
                strm.ReadFrom(null, bakFile, cancellationToken);
                Levels.Add(key, strm);
                levelPaths.Add(key, levelPath);
            }
        }

        if (SoundsPath is null)
            throw new DirectoryNotFoundException("Sounds folder could not be found.");

        if ((Voice == SonicCloneVoices.Auto && Mode == SonicClones.Sonic)
            || Voice == SonicCloneVoices.Sonic) {
            Console.WriteLine("Reverting voices...");

            foreach (var f in Directory.GetFiles(SoundsPath)) {
                if (f.EndsWith(".acb.bak"))
                    File.Move(f, f[..^4], true);
                else if (f.EndsWith(".awb.bak"))
                    File.Move(f, f[..^4], true);
            }

            foreach (var f in Directory.GetFiles(Path.Join(SoundsPath, "exertions"))) {
                if (f.EndsWith(".acb.bak"))
                    File.Move(f, f[..^4], true);
                else if (f.EndsWith(".awb.bak"))
                    File.Move(f, f[..^4], true);
            }
        } else {
            Console.WriteLine("Patching voices...");
            await PatchSonicDialogues(cancellationToken);
            await PatchSonicExertions(cancellationToken);
        }

        if (Mode != SonicClones.Sonic) {
            if (Heroes is null || HeroesPath is null)
                throw new FileNotFoundException("heroes.wiiu.stream could not be found.");

            Console.WriteLine("Patching graphics...");

            if (Mode == SonicClones.Shadow)
                await PatchSpinDashBallColor(cancellationToken);

            await Task.WhenAll(Levels.Values.Select(level => PatchSonicModel(level, cancellationToken)));
            await PatchAddCloneTextures(cancellationToken);

            Console.WriteLine("Saving data...");
            var saveConfig = new WiiuStreamFile.SaveConfig {
                CompressionLevel = CompressionLevel,
                CompressionChunkSize = CompressionChunkSize,
            };

            var suppressProgressDuration = TimeSpan.FromSeconds(5);
            await CompressProgramCommand.WriteAndPrintProgress(
                HeroesPath,
                Heroes,
                saveConfig,
                cancellationToken,
                suppressProgressDuration);
            foreach (var (levelName, level) in Levels)
                await CompressProgramCommand.WriteAndPrintProgress(
                    levelPaths[levelName],
                    level,
                    saveConfig,
                    cancellationToken,
                    suppressProgressDuration);
        }

        Console.WriteLine("Done!");

        return 0;
    }

    private async Task PatchSpinDashBallColor(
        CancellationToken cancellationToken) {
        // Turn Sonic's blue spindash ball grey
        var decoder = new BcDecoder();
        var encoder = new BcEncoder {
            OutputOptions = {
                GenerateMipMaps = false,
                Quality = CompressionQuality.BestQuality,
                FileFormat = OutputFileFormat.Dds,
            }
        };

        using var ms = new MemoryStream();
        foreach (var (filename, divisor) in DesaturationTargetTextures) {
            var entry = Heroes!.GetEntry(filename);
            var image = await decoder.DecodeToImageRgba32Async(
                new MemoryStream(entry.Source.ReadRaw()),
                cancellationToken);

            var hasAlpha = false;
            image.ProcessPixelRows(
                row => {
                    for (var y = 0; y < row.Height; y++) {
                        var span = row.GetRowSpan(y);
                        for (var i = 0; i < span.Length; i++) {
                            span[i].R = span[i].G = span[i].B =
                                (byte) ((span[i].R + span[i].G + span[i].B) / divisor);
                            hasAlpha |= span[i].A != 255;
                        }
                    }
                });
            encoder.OutputOptions.Format = hasAlpha ? CompressionFormat.Bc3 : CompressionFormat.Bc1;

            ms.Position = 0;
            ms.SetLength(0);
            await encoder.EncodeToStreamAsync(image, ms, cancellationToken);
            entry.Source = new(ms.ToArray());
        }
    }

    private Task PatchSonicDialogues(CancellationToken cancellationToken) => Task.Run(
        () => {
            Debug.Assert(SoundsPath is not null);
            foreach (var f in Directory.GetFiles(SoundsPath)) {
                if (!f.EndsWith(".acb"))
                    continue;

                var baseName = f[..^4];
                var acbFile = baseName + ".acb";
                var awbFile = File.Exists(baseName + ".awb") ? baseName + ".awb" : null;

                var targetAcb = new AcbFile(
                    File.Exists(acbFile + ".bak") ? acbFile + ".bak" : acbFile,
                    awbFile is not null && File.Exists(awbFile + ".bak") ? awbFile + ".bak" : awbFile);

                // silence all sonic dialogues
                var changed = false;
                foreach (var (cueName, sequenceIndex) in targetAcb.CueNameToSequenceIndex) {
                    if (!cueName.Contains("_Sonic_x_x_"))
                        continue;

                    foreach (var trackIndex in targetAcb.GetTrackIndices(sequenceIndex))
                        targetAcb.SilenceTrack(trackIndex);
                    changed = true;
                }

                if (!changed)
                    continue;

                if (!File.Exists(acbFile + ".bak"))
                    File.Copy(acbFile, acbFile + ".bak");
                if (awbFile is not null && !File.Exists(awbFile + ".bak"))
                    File.Copy(awbFile, awbFile + ".bak");

                targetAcb.Save(baseName);
                Console.WriteLine("Silenced Sonic dialogues in acb/awb file(s): {0}", baseName);
            }
        },
        cancellationToken);

    private Task PatchSonicExertions(CancellationToken cancellationToken) => Task.Run(
        () => {
            Debug.Assert(SoundsPath is not null);
            var baseName = Path.Join(SoundsPath, "exertions/hero_sonic");
            var acbFile = baseName + ".acb";
            var awbFile = File.Exists(baseName + ".awb") ? baseName + ".awb" : null;
            if (!File.Exists(acbFile + ".bak"))
                File.Copy(acbFile, baseName + ".acb.bak");
            if (awbFile is not null && !File.Exists(awbFile + ".bak"))
                File.Copy(awbFile, awbFile + ".bak");
            var targetAcb = new AcbFile(acbFile + ".bak", awbFile + ".bak");

            switch (Voice) {
                case SonicCloneVoices.Auto when Mode == SonicClones.Shadow:
                case SonicCloneVoices.Shadow: {
                    var shadowAcb = new AcbFile(Path.Join(SoundsPath, "exertions", "shadow.acb"), null);
                    var trackIndicesOfShadowWaveformsInSonic = new List<ushort>(shadowAcb.InternalWaveforms.Count);
                    trackIndicesOfShadowWaveformsInSonic.AddRange(
                        shadowAcb.InternalWaveformRows.Select(
                            row => targetAcb.AddTrack(
                                row,
                                shadowAcb.InternalWaveforms[row.GetValue<ushort>("Id")])));

                    // 1. copy all shadow voices to sonic voices file
                    var translatedTrackIndices = new Dictionary<string, List<ushort>>();
                    foreach (var (key, indices) in shadowAcb.RelevantWaveformIds) {
                        var translated = translatedTrackIndices[key] = new();
                        translated.AddRange(indices.Select(index => trackIndicesOfShadowWaveformsInSonic[index]));
                    }

                    // 2. silence all sonic voices
                    for (var i = 0; i < targetAcb.SequenceTable.Rows.Count; i++)
                        targetAcb.SetTrackIndices(i, Array.Empty<ushort>());

                    // 3. for the voice lines with matching names, put shadow lines
                    foreach (var (cueName, sequenceIndex) in targetAcb.CueNameToSequenceIndex) {
                        var shadowCueName = "Shadow_" + cueName.Split("_", 2)[1];
                        if (!translatedTrackIndices.TryGetValue(shadowCueName, out var indices))
                            continue;
                        targetAcb.SetTrackIndices(sequenceIndex, indices.ToArray());
                    }

                    // 4. apply extra mappings
                    foreach (var (from, to) in SonicToShadowExertionMap) {
                        var sequenceIndex = targetAcb.CueNameToSequenceIndex[from];
                        targetAcb.SetTrackIndices(
                            sequenceIndex,
                            to == "" ? Array.Empty<ushort>() : translatedTrackIndices[to].ToArray());
                    }

                    break;
                }
                case SonicCloneVoices.Auto when Mode == SonicClones.MetalSonic:
                case SonicCloneVoices.Silence: {
                    // he's a silent type
                    for (var i = 0; i < targetAcb.SequenceTable.Rows.Count; i++)
                        targetAcb.SetTrackIndices(i, Array.Empty<ushort>());
                    break;
                }
                case SonicCloneVoices.Sonic:
                default:
                    throw new InvalidOperationException();
            }

            targetAcb.Save(baseName);
        },
        cancellationToken);

    private Task PatchAddCloneTextures(CancellationToken cancellationToken) => Task.Run(
        () => {
            foreach (var entry in ReferenceLevel.Entries.Where(
                         entry => entry.Header.InnerPath.StartsWith(
                             ReferenceTexturePathPrefix,
                             StringComparison.InvariantCultureIgnoreCase))) {
                Heroes!.PutEntry(-1, entry.Header.InnerPath, entry.Source);
            }
        },
        cancellationToken);

    private Task PatchSonicModel(WiiuStreamFile level, CancellationToken cancellationToken) => Task.Run(
        () => {
            var sonicChr = level.GetEntry($"{SonicBaseName}.chr");
            var replacementChr = ReferenceLevel.GetEntry($"{ReferenceObjectBaseName}.chr");
            sonicChr.Source = replacementChr.Source;

            var sonicMtl = level.GetEntry($"{SonicBaseName}.mtl", SkinFlag.Sonic_Default);
            var sonicMtlAlt = level.GetEntry($"{SonicBaseName}.mtl", SkinFlag.Sonic_Alt);
            var replacementMtl = ReferenceLevel.GetEntry($"{ReferenceObjectBaseName}.mtl");

            var sonicDoc = PbxmlFile.Load(sonicMtl.Source.ReadRaw());
            var replacementDoc = PbxmlFile.Load(replacementMtl.Source.ReadRaw());
            var existingNames = replacementDoc["Material"]!["SubMaterials"]!
                .OfType<XmlElement>()
                .Select(x => x.GetAttribute("Name"))
                .ToHashSet();
            foreach (var elem in sonicDoc["Material"]!["SubMaterials"]!.OfType<XmlElement>()) {
                if (existingNames.Contains(elem.GetAttribute("Name")))
                    continue;

                replacementDoc["Material"]!["SubMaterials"]!.AppendChild(replacementDoc.ImportNode(elem, true));
                existingNames.Add(elem.GetAttribute("Name"));
            }

            using var targetMs = new MemoryStream();
            PbxmlFile.Pack(replacementDoc, new(targetMs));
            sonicMtl.Source = sonicMtlAlt.Source = new(targetMs.ToArray());
        },
        cancellationToken);

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum SonicClones {
        Sonic = 0,
        Shadow = 1,
        MetalSonic = 2,

        // Aliases for command line invocation
        Default = Sonic,
        Revert = Sonic,
        Restore = Sonic,
        Metal = MetalSonic,
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum SonicCloneVoices {
        Auto = 0,
        Sonic = 1,
        Shadow = 2,
        Silence = 3,

        // Aliases for command line invocation
        Default = Auto,
        Revert = Auto,
        Restore = Auto,
    }
}
