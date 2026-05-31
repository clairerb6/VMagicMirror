using MahApps.Metro.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace Baku.VMagicMirrorConfig.View
{
    public partial class SettingWindow : MetroWindow
    {
        private const int InitialPositionOffset = 48;

        public SettingWindow() => InitializeComponent();

        /// <summary>現在設定ウィンドウがあればそれを取得し、なければnullを取得します。</summary>
        public static SettingWindow? CurrentWindow { get; private set; } = null;

        public static void OpenOrActivateExistingWindow()
        {
            if (CurrentWindow == null)
            {
                CurrentWindow = new SettingWindow()
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                };

                SetInitialPosition(CurrentWindow);
                CurrentWindow.Closed += OnSettingWindowClosed;
                CurrentWindow.Show();
            }
            else
            {
                CurrentWindow.Activate();
            }
        }

        private static async void OnSettingWindowClosed(object? sender, EventArgs e)
        {
            if (CurrentWindow != null)
            {
                CurrentWindow.Closed -= OnSettingWindowClosed;
                CurrentWindow = null;

                //NOTE: 設定ウィンドウを閉じたあとはGC可能なリソースがそこそこある(WindowとかViewModelとか)ので、明示的にやってしまう
                await Task.Delay(1000);
                GC.Collect();
            }
        }

        private static void SetInitialPosition(SettingWindow window)
        {
            if (window.Owner == null)
            {
                return;
            }

            var left = window.Owner.Left + InitialPositionOffset;
            var top = window.Owner.Top + InitialPositionOffset;

            var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - window.Width;
            var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - window.Height;

            window.Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(left, maxLeft));
            window.Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(top, maxTop));
        }
    }
}
