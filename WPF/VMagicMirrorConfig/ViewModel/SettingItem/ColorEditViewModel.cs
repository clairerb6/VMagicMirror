using System.Windows.Media;

namespace Baku.VMagicMirrorConfig.ViewModel
{
    public sealed class ColorEditViewModel : ViewModelBase
    {
        public ColorEditViewModel() : this(
            new RgbColorBinding(
                new RProperty<int>(255),
                new RProperty<int>(255),
                new RProperty<int>(255)
            ),
            "Color"
        )
        {
        }

        public ColorEditViewModel(RgbColorBinding rgb, string title)
        {
            Rgb = rgb;
            Title = title;
            CloseCommand = new ActionCommand(() => DialogResult = true);
        }

        private bool? _dialogResult = null;

        public string Title { get; }
        public RgbColorBinding Rgb { get; }
        public ActionCommand CloseCommand { get; }

        public bool? DialogResult
        {
            get => _dialogResult;
            private set => SetValue(ref _dialogResult, value);
        }

        public Color PreviewColor => Rgb.Color;
    }
}
