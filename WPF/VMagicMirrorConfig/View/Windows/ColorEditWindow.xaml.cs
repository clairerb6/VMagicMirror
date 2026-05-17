using MahApps.Metro.Controls;
using Baku.VMagicMirrorConfig.ViewModel;
using System;
using System.Windows;

namespace Baku.VMagicMirrorConfig.View
{
    public partial class ColorEditWindow : MetroWindow
    {
        private static ColorEditWindow? _currentWindow = null;

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

        public static void ShowColorWindow(Window? owner, RgbColorBinding rgb, string title)
        {
            if (_currentWindow != null)
            {
                _currentWindow.DataContext = new ColorEditViewModel(rgb, title);

                if (_currentWindow.WindowState == WindowState.Minimized)
                {
                    _currentWindow.WindowState = WindowState.Normal;
                }

                _currentWindow.Activate();
                return;
            }

            var window = new ColorEditWindow()
            {
                Owner = owner,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                DataContext = new ColorEditViewModel(rgb, title),
            };

            _currentWindow = window;
            window.Closed += OnColorEditWindowClosed;
            window.Show();
        }

        private static void OnColorEditWindowClosed(object? sender, EventArgs e)
        {
            if (sender is ColorEditWindow window)
            {
                window.Closed -= OnColorEditWindowClosed;
            }

            if (ReferenceEquals(_currentWindow, sender))
            {
                _currentWindow = null;
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException)
            {
                Close();
            }
        }
    }
}
