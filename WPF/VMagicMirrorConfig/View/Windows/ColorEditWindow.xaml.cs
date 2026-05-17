using MahApps.Metro.Controls;
using Baku.VMagicMirrorConfig.ViewModel;
using System.Windows;

namespace Baku.VMagicMirrorConfig.View
{
    public partial class ColorEditWindow : MetroWindow
    {
        public ColorEditWindow() => InitializeComponent();

        public static bool? ShowColorDialog(Window? owner, RgbColorBinding rgb, string title)
        {
            var window = new ColorEditWindow()
            {
                Owner = owner,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                DataContext = new ColorEditViewModel(rgb, title),
            };

            return window.ShowDialog();
        }
    }
}
