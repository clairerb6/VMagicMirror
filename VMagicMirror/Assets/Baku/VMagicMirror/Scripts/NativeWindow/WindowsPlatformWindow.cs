using System.Collections.Generic;
using UnityEngine;

namespace Baku.VMagicMirror
{
    public class WindowsPlatformWindow : IPlatformWindow
    {
        public bool SupportsNativeWindowControl => true;

        public uint GetWindowStyle() =>
            NativeMethods.GetWindowLong(NativeMethods.GetUnityWindowHandle(), NativeMethods.GWL_STYLE);

        public uint GetExWindowStyle() =>
            NativeMethods.GetWindowLong(NativeMethods.GetUnityWindowHandle(), NativeMethods.GWL_EXSTYLE);

        public void SetWindowStyle(uint style) =>
            NativeMethods.SetWindowLong(NativeMethods.GetUnityWindowHandle(), NativeMethods.GWL_STYLE, style);

        public void SetExWindowStyle(uint style) =>
            NativeMethods.SetWindowLong(NativeMethods.GetUnityWindowHandle(), NativeMethods.GWL_EXSTYLE, style);

        public bool TryGetWindowRect(out NativeMethods.RECT rect) =>
            NativeMethods.GetWindowRect(NativeMethods.GetUnityWindowHandle(), out rect);

        public Vector2Int GetWindowPosition() => NativeMethods.GetUnityWindowPosition();
        public void SetWindowPosition(int x, int y) => NativeMethods.SetUnityWindowPosition(x, y);
        public void SetWindowSize(int width, int height) => NativeMethods.SetUnityWindowSize(width, height);
        public void RefreshWindowSize(int width, int height) => NativeMethods.RefreshWindowSize(width, height);
        public void SetTopMost(bool enable) => NativeMethods.SetUnityWindowTopMost(enable);
        public void SetTransparent(bool enable) => NativeMethods.SetDwmTransparent(enable);
        public void SetWindowAlpha(byte alpha) => NativeMethods.SetWindowAlpha(alpha);
        public void SetActive() => NativeMethods.SetUnityWindowActive();
        public NativeMethods.RECT GetPrimaryWindowRect() => NativeMethods.GetPrimaryWindowRect();
        public List<NativeMethods.RECT> LoadAllMonitorRects() => NativeMethods.LoadAllMonitorRects();
        public Vector2Int GetMousePosition() => NativeMethods.GetWindowsMousePosition();
    }
}
