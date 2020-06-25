using System.Collections.Generic;
using System.Windows.Forms;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public class CustomButtonEntryCombined : NotifyPropertyChanged {
        [CanBeNull]
        public CustomButtonEntry Button { get; }

        [NotNull]
        public WheelButtonEntry WheelButton { get; }

        public WheelButtonEntry WheelButtonModifier { get; }

        public string ToolTip { get; }

        public CustomButtonEntryCombined([LocalizationRequired(false)] string id, string displayName,
                string toolTip, Keys? defaultKey, [CanBeNull] List<Keys> modifiers) {
            WheelButton = new WheelButtonEntry(id, displayName, true);
            WheelButtonModifier = new WheelButtonEntry(id, displayName, false, true);
            WheelButton.ModifierButton = WheelButtonModifier;
            WheelButtonModifier.ModifierButton = WheelButton;
            Button = new CustomButtonEntry(id, displayName, defaultKey, modifiers);
            ToolTip = toolTip;
        }
    }
}