﻿using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Data;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers.AcSettings {
    public class SystemSettings : IniSettings {
        internal SystemSettings() : base("assetto_corsa", systemConfig: true) {
            PatchHelper.Reloaded += (sender, args) => { ScreenshotFormats = DefaultScreenshotFormats().ToList(); };
        }

        public static IEnumerable<SettingEntry> DefaultScreenshotFormats() {
            return new[] {
                new SettingEntry("JPG", ToolsStrings.AcSettings_ScreenshotFormat_Jpeg),
                new SettingEntry("BMP", ToolsStrings.AcSettings_ScreenshotFormat_Bmp),
                new SettingEntry("PNG", "PNG (requires Custom Shaders Patch)")
            };
            /*.Concat(PatchHelper.IsFeatureSupported(PatchHelper.FeatureExtraScreenshotFormats)
                    ? PatchHelper.GetConfig("data_manifest.ini")["FEATURES"].GetStrings("SUPPORTED_SCREENSHOT_FORMATS")
                            .Select(x => new SettingEntry(x, $"{x} (added by Custom Shaders Patch)"))
                    : new SettingEntry[0]);*/
        }

        private List<SettingEntry> _screenshotFormats = DefaultScreenshotFormats().ToList();

        public IReadOnlyList<SettingEntry> ScreenshotFormats {
            get => _screenshotFormats;
            set {
                if (value.SequenceEqual(_screenshotFormats)) return;
                _screenshotFormats = value.ToList();
                OnPropertyChanged(false);
                ScreenshotFormat = _screenshotFormats.GetByIdOrDefault(ScreenshotFormat?.Id) ?? _screenshotFormats.FirstOrDefault();
            }
        }

        #region Miscellaneous
        private int _simulationValue;

        public int SimulationValue {
            get => _simulationValue;
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _simulationValue)) return;
                _simulationValue = value;
                OnPropertyChanged();
            }
        }

        private bool _developerApps;

        public bool DeveloperApps {
            get => _developerApps;
            set => Apply(value, ref _developerApps);
        }

        private bool _hideDriver;

        public bool HideDriver {
            get => _hideDriver;
            set => Apply(value, ref _hideDriver);
        }

        private bool _allowFreeCamera;

        public bool AllowFreeCamera {
            get => _allowFreeCamera;
            set => Apply(value, ref _allowFreeCamera);
        }

        private bool _logging;

        public bool Logging {
            get => _logging;
            set => Apply(value, ref _logging);
        }

        private SettingEntry _screenshotFormat;

        public SettingEntry ScreenshotFormat {
            get => _screenshotFormat;
            set {
                if (!ScreenshotFormats.Contains(value)) value = ScreenshotFormats[0];
                if (Equals(value, _screenshotFormat)) return;
                _screenshotFormat = value;
                OnPropertyChanged();
            }
        }

        public int MirrorsFieldOfViewDefault => 10;

        private int _mirrorsFieldOfView;

        public int MirrorsFieldOfView {
            get => _mirrorsFieldOfView;
            set {
                value = value.Clamp(1, 180);
                if (Equals(value, _mirrorsFieldOfView)) return;
                _mirrorsFieldOfView = value;
                OnPropertyChanged();
            }
        }

        public int MirrorsFarPlaneDefault => 400;

        private int _mirrorsFarPlane;

        public int MirrorsFarPlane {
            get => _mirrorsFarPlane;
            set {
                value = value.Clamp(10, 2000);
                if (Equals(value, _mirrorsFarPlane)) return;
                _mirrorsFarPlane = value;
                OnPropertyChanged();
            }
        }

        private bool _vrCameraShake;

        public bool VrCameraShake {
            get => _vrCameraShake;
            set => Apply(value, ref _vrCameraShake);
        }
        #endregion

        #region Experimental FFB
        private static readonly Busy _ffbSettingsBusy = new Busy();

        private static void OnSystemFfbSettingsChanged() {
            _ffbSettingsBusy.Do(() => { AcSettingsHolder.Controls.OnSystemFfbSettingsChanged(); });
        }

        private bool _softLock;

        public bool SoftLock {
            get => _softLock;
            set => Apply(value, ref _softLock, OnSystemFfbSettingsChanged);
        }

        private bool _ffbGyro;

        public bool FfbGyro {
            get => _ffbGyro;
            set => Apply(value, ref _ffbGyro, OnSystemFfbSettingsChanged);
        }

        private int _ffbDamperMinLevel;

        public int FfbDamperMinLevel {
            get => _ffbDamperMinLevel;
            set => Apply(value.Clamp(0, 100), ref _ffbDamperMinLevel, OnSystemFfbSettingsChanged);
        }

        private int _ffbDamperGain;

        public int FfbDamperGain {
            get => _ffbDamperGain;
            set => Apply(value.Clamp(0, 100), ref _ffbDamperGain, OnSystemFfbSettingsChanged);
        }
        #endregion

        public void LoadFfbFromIni(IniFile ini) {
            using (_ffbSettingsBusy.Set()) {
                SoftLock = ini["SOFT_LOCK"].GetBool("ENABLED", false);
                VrCameraShake = ini["VR"].GetBool("ENABLE_CAMERA_SHAKE", false);
                //FfbSkipSteps = ini["FORCE_FEEDBACK"].GetInt("FF_SKIP_STEPS", 1);

                var section = ini["FF_EXPERIMENTAL"];
                FfbGyro = section.GetBool("ENABLE_GYRO", false);
                FfbDamperMinLevel = section.GetDouble("DAMPER_MIN_LEVEL", 0d).ToIntPercentage();
                FfbDamperGain = section.GetDouble("DAMPER_GAIN", 1d).ToIntPercentage();
            }
        }

        public void SaveFfbToIni(IniFile ini) {
            ini["SOFT_LOCK"].Set("ENABLED", SoftLock);
            ini["VR"].Set("ENABLE_CAMERA_SHAKE", VrCameraShake);
            //ini["FORCE_FEEDBACK"].Set("FF_SKIP_STEPS", FfbSkipSteps);

            var section = ini["FF_EXPERIMENTAL"];
            section.Set("ENABLE_GYRO", FfbGyro);
            section.Set("DAMPER_MIN_LEVEL", FfbDamperMinLevel.ToDoublePercentage());
            section.Set("DAMPER_GAIN", FfbDamperGain.ToDoublePercentage());
        }

        public void ImportFfb(string serialized) {
            if (string.IsNullOrWhiteSpace(serialized)) return;
            LoadFfbFromIni(IniFile.Parse(serialized));
        }

        public string ExportFfb() {
            var ini = new IniFile();
            SaveFfbToIni(ini);
            return ini.ToString();
        }

        protected override void LoadFromIni() {
            LoadFfbFromIni(Ini);

            SimulationValue = Ini["ASSETTO_CORSA"].GetDouble("SIMULATION_VALUE", 0d).ToIntPercentage();
            DeveloperApps = Ini["AC_APPS"].GetBool("ENABLE_DEV_APPS", false);
            AllowFreeCamera = Ini["CAMERA"].GetBool("ALLOW_FREE_CAMERA", false);
            Logging = !Ini["LOG"].GetBool("SUPPRESS", false);
            HideDriver = Ini["DRIVER"].GetBool("HIDE", false);
            ScreenshotFormat = Ini["SCREENSHOT"].GetOrCreateEntry("FORMAT", _screenshotFormats, v => $"{v} (not supported)");
            MirrorsFieldOfView = Ini["MIRRORS"].GetInt("FOV", MirrorsFieldOfViewDefault);
            MirrorsFarPlane = Ini["MIRRORS"].GetInt("FAR_PLANE", MirrorsFarPlaneDefault);
        }

        protected override void SetToIni() {
            SaveFfbToIni(Ini);

            Ini["ASSETTO_CORSA"].Set("SIMULATION_VALUE", SimulationValue.ToDoublePercentage());
            Ini["AC_APPS"].Set("ENABLE_DEV_APPS", DeveloperApps);
            Ini["CAMERA"].Set("ALLOW_FREE_CAMERA", AllowFreeCamera);
            Ini["LOG"].Set("SUPPRESS", !Logging);
            Ini["DRIVER"].Set("HIDE", HideDriver);
            Ini["SCREENSHOT"].Set("FORMAT", ScreenshotFormat);
            Ini["MIRRORS"].Set("FOV", MirrorsFieldOfView);
            Ini["MIRRORS"].Set("FAR_PLANE", MirrorsFarPlane);
        }
    }

    public class SystemOptionsSettings : IniSettings {
        internal SystemOptionsSettings() : base("options", systemConfig: true) { }

        private bool _ignoreResultTeleport;

        public bool IgnoreResultTeleport {
            get => _ignoreResultTeleport;
            set => Apply(value, ref _ignoreResultTeleport);
        }

        protected override void LoadFromIni() {
            var section = Ini["OPTIONS"];
            IgnoreResultTeleport = section.GetBool("IGNORE_RESULT_TELEPORT", false);
        }

        protected override void SetToIni() {
            var section = Ini["OPTIONS"];
            section.Set("IGNORE_RESULT_TELEPORT", IgnoreResultTeleport);
        }
    }
}