using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Baku.VMagicMirrorConfig.ViewModel
{
    public sealed class RgbColorBinding : NotifiableBase
    {
        public RgbColorBinding(RProperty<int> r, RProperty<int> g, RProperty<int> b)
        {
            R = r;
            G = g;
            B = b;

            R.AddWeakEventHandler(OnRgbValueChanged);
            G.AddWeakEventHandler(OnRgbValueChanged);
            B.AddWeakEventHandler(OnRgbValueChanged);
        }

        public RProperty<int> R { get; }
        public RProperty<int> G { get; }
        public RProperty<int> B { get; }

        public Color Color
        {
            get => Color.FromRgb(
                ClampToByte(R.Value),
                ClampToByte(G.Value),
                ClampToByte(B.Value)
            );
            set
            {
                R.Value = value.R;
                G.Value = value.G;
                B.Value = value.B;
                RaisePropertyChanged();
            }
        }

        private void OnRgbValueChanged(object? sender, PropertyChangedEventArgs e)
            => RaisePropertyChanged(nameof(Color));

        private static byte ClampToByte(int value) => (byte)Math.Clamp(value, 0, 255);
    }
}
