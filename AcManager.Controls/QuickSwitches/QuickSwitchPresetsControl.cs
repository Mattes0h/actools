using System.Windows;
using System.Windows.Media;

namespace AcManager.Controls.QuickSwitches {
    public class QuickSwitchPresetsControl : UserPresetsControl {
        static QuickSwitchPresetsControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickSwitchPresetsControl), new FrameworkPropertyMetadata(typeof(QuickSwitchPresetsControl)));
        }

        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(nameof(IconData), typeof(Geometry),
                typeof(QuickSwitchPresetsControl));

        public Geometry IconData {
            get => (Geometry)GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }
    }
}