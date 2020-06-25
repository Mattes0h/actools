﻿using System.Linq;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class GameplaySettings : IniSettings {
        public SettingEntry[] UnitsTypes { get; } = {
            new SettingEntry("0", ToolsStrings.Gameplay_Units_Metrical),
            new SettingEntry("1", ToolsStrings.Gameplay_Units_Imperial)
        };

        internal GameplaySettings() : base(@"gameplay") {}

        #region GUI
        private SettingEntry _units;

        public SettingEntry Units {
            get => _units;
            set {
                if (!UnitsTypes.Contains(value)) value = UnitsTypes[0];
                if (Equals(value, _units)) return;
                _units = value;
                OnPropertyChanged();
            }
        }

        private bool _allowOverlapping;

        public bool AllowOverlapping {
            get => _allowOverlapping;
            set => Apply(value, ref _allowOverlapping);
        }

        private bool _displayTimeGap;

        public bool DisplayTimeGap {
            get => _displayTimeGap;
            set => Apply(value, ref _displayTimeGap);
        }

        private bool _displayDamage;

        public bool DisplayDamage {
            get => _displayDamage;
            set => Apply(value, ref _displayDamage);
        }

        private bool _displayLeaderboard;

        public bool DisplayLeaderboard {
            get => _displayLeaderboard;
            set => Apply(value, ref _displayLeaderboard);
        }

        private bool _displayMirror;

        public bool DisplayMirror {
            get => _displayMirror;
            set => Apply(value, ref _displayMirror);
        }

        private bool _displayDriverNames;

        public bool DisplayDriverNames {
            get => _displayDriverNames;
            set => Apply(value, ref _displayDriverNames);
        }

        private bool _downshiftProtectionNotification;

        public bool DownshiftProtectionNotification {
            get => _downshiftProtectionNotification;
            set => Apply(value, ref _downshiftProtectionNotification);
        }
        #endregion

        private int _steeringWheelLimit;

        public int SteeringWheelLimit {
            get => _steeringWheelLimit;
            set => Apply(value.Clamp(0, 450), ref _steeringWheelLimit);
        }

        private bool _enablePythonApps;

        public bool EnablePythonApps {
            get => _enablePythonApps;
            set => Apply(value, ref _enablePythonApps);
        }

        protected override void LoadFromIni() {
            Units = Ini["OPTIONS"].GetEntry("USE_MPH", UnitsTypes);
            EnablePythonApps = Ini["PYTHON"].GetBool("ENABLE_PYTHON", false);
            DisplayTimeGap = Ini["TIME_DIFFERENCE"].GetBool("IS_ACTIVE", true);
            DisplayDamage = Ini["DAMAGE_DISPLAYER"].GetBool("IS_ACTIVE", true);
            DisplayLeaderboard = Ini["OVERLAY_LEADERBOARD"].GetBool("ACTIVE", true);
            DisplayMirror = Ini["VIRTUAL_MIRROR"].GetBool("ACTIVE", true);
            DisplayDriverNames = Ini["DRIVER_NAME_DISPLAYER"].GetBool("IS_ACTIVE", false);
            AllowOverlapping = Ini["GUI"].GetBool("ALLOW_OVERLAPPING_FORMS", true);
            DownshiftProtectionNotification = Ini["DOWNSHIFT_PROTECTION_NOTIFICATION"].GetBool("ACTIVE", true);
            SteeringWheelLimit = Ini["STEER_ANIMATION"].GetInt("MAX_DEGREES", 0);
        }

        protected override void SetToIni() {
            Ini["OPTIONS"].Set("USE_MPH", Units);
            Ini["PYTHON"].Set("ENABLE_PYTHON", EnablePythonApps);
            Ini["TIME_DIFFERENCE"].Set("IS_ACTIVE", DisplayTimeGap);
            Ini["DAMAGE_DISPLAYER"].Set("IS_ACTIVE", DisplayDamage);
            Ini["OVERLAY_LEADERBOARD"].Set("ACTIVE", DisplayLeaderboard);
            Ini["VIRTUAL_MIRROR"].Set("ACTIVE", DisplayMirror);
            Ini["DRIVER_NAME_DISPLAYER"].Set("IS_ACTIVE", DisplayDriverNames);
            Ini["GUI"].Set("ALLOW_OVERLAPPING_FORMS", AllowOverlapping);
            Ini["DOWNSHIFT_PROTECTION_NOTIFICATION"].Set("ACTIVE", DownshiftProtectionNotification);
            Ini["STEER_ANIMATION"].Set("MAX_DEGREES", SteeringWheelLimit);
        }
    }
}