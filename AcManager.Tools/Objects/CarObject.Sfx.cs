﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private string _guidsFilename;
        public string GuidsFilename => _guidsFilename ?? (_guidsFilename = Path.Combine(Location, @"sfx", @"GUIDs.txt"));

        private string _soundbankFilename;
        public string SoundbankFilename => _soundbankFilename ?? (_soundbankFilename = Path.Combine(Location, @"sfx", $@"{Id}.bank"));

        private static Regex _guidsRegex;
        private static Dictionary<string, string> _kunosGuids;

        private async Task ReadKunosGuidsAsync() {
            if (_guidsRegex == null) {
                _guidsRegex = new Regex(@"^\{(\w{8}(?:-\w{4}){3}-\w{12})\}\s+event:/cars/(\w+)/e", RegexOptions.Compiled);
            }

            _kunosGuids = new Dictionary<string, string>();

            var filename = AcPaths.GetSfxGuidsFilename(AcRootDirectory.Instance.RequireValue);
            if (File.Exists(filename)) {
                var lines = await FileUtils.ReadAllLinesAsync(filename).ConfigureAwait(false);
                for (var i = 0; i < lines.Length; i++) {
                    var m = _guidsRegex.Match(lines[i]);
                    if (m.Success) {
                        _kunosGuids[m.Groups[1].Value] = m.Groups[2].Value;
                    }
                }
            }
        }

        private bool _soundDonorSet;
        private string _soundDonorId;

        [CanBeNull]
        public string SoundDonorId => _soundDonorId;

        [CanBeNull]
        public CarObject SoundDonor => _soundDonor?.Value;
        private Lazier<CarObject> _soundDonor;

        [ItemCanBeNull]
        public async Task<string> GetSoundOrigin() {
            if (_soundDonorSet) return _soundDonorId;
            _soundDonorSet = true;

            try {
                if (_kunosGuids == null) {
                    await ReadKunosGuidsAsync().ConfigureAwait(false);
                    if (_kunosGuids == null) return null;
                }

                if (!File.Exists(GuidsFilename)) {
                    // TODO
                    return null;
                }

                var lines = await FileUtils.ReadAllLinesAsync(GuidsFilename).ConfigureAwait(false);

                foreach (var line in lines) {
                    var m = _guidsRegex.Match(line);
                    if (m.Success && m.Groups[2].Value == Id && _kunosGuids.TryGetValue(m.Groups[1].Value, out _soundDonorId)) {
                        _soundDonor = new Lazier<CarObject>(() => SoundDonorId == null ? null : CarsManager.Instance.GetById(SoundDonorId));
                        OnPropertyChanged(nameof(SoundDonorId));
                        OnPropertyChanged(nameof(SoundDonor));
                        return _soundDonorId;
                    }
                }

                return null;
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        public static void ReplaceSound(CarObject donor, [NotNull] string target) {
            var guids = donor.GuidsFilename;
            var soundbank = donor.SoundbankFilename;

            var id = Path.GetFileName(target);
            var newGuids = Path.Combine(target, @"sfx", @"GUIDs.txt");
            var newSoundbank = Path.Combine(target, @"sfx", $@"{id}.bank");
            FileUtils.EnsureDirectoryExists(Path.Combine(target, @"sfx"));

            using (var putGuids = FileUtils.RecycleOriginal(newGuids))
            using (var putSoundbank = FileUtils.RecycleOriginal(newSoundbank)) {
                if (File.Exists(guids) && File.Exists(soundbank)) {
                    FileUtils.HardLinkOrCopy(soundbank, putSoundbank.Filename);
                    File.WriteAllText(putGuids.Filename, File.ReadAllText(guids).Replace(donor.Id, id));
                } else if (File.Exists(soundbank) && donor.Author == AuthorKunos) {
                    FileUtils.HardLinkOrCopy(soundbank, putSoundbank.Filename);
                    File.WriteAllText(putGuids.Filename, File.ReadAllLines(AcPaths.GetSfxGuidsFilename(AcRootDirectory.Instance.RequireValue))
                                                    .Where(x => !x.Contains(@"} bank:/") || x.Contains(@"} bank:/common") ||
                                                            x.EndsWith(@"} bank:/" + donor.Id))
                                                    .Where(x => !x.Contains(@"} event:/") || x.Contains(@"} event:/cars/" + donor.Id + @"/"))
                                                    .JoinToString(Environment.NewLine).Replace(donor.Id, id));
                } else {
                    throw new InformativeException(ToolsStrings.Car_ReplaceSound_WrongCar, ToolsStrings.Car_ReplaceSound_WrongCar_Commentary);
                }
            }
        }

        public async Task ReplaceSound(CarObject donor) {
            if (string.Equals(donor.Id, Id, StringComparison.OrdinalIgnoreCase)) {
                NonfatalError.Notify(ToolsStrings.Car_ReplaceSound_CannotReplace, "Source and destination are the same.");
                return;
            }

            try {
                using (WaitingDialog.Create("Replacing…")) {
                    await Task.Run(() => ReplaceSound(donor, Location));
                }
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.Car_ReplaceSound_CannotReplace, ToolsStrings.Car_ReplaceSound_CannotReplace_Commentary, e);
            }
        }
    }
}
