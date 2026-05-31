using System.Collections.Generic;
using UnityEngine;

namespace Baku.VMagicMirror
{
    public class LinuxPlatformWindow : IPlatformWindow
    {
        public bool SupportsNativeWindowControl => false;

        public uint GetWindowStyle() => 0;
        public uint GetExWindowStyle() => 0;
        public void SetWindowStyle(uint style) { }
        public void SetExWindowStyle(uint style) { }

        public bool TryGetWindowRect(out NativeMethods.RECT rect)
        {
            rect = new NativeMethods.RECT
            {
                left = 0,
                top = 0,
                right = Screen.width,
                bottom = Screen.height,
            };
            return true;
        }

        public Vector2Int GetWindowPosition() => Vector2Int.zero;
        public void SetWindowPosition(int x, int y) { }
        public void SetWindowSize(int width, int height) { }
        public void RefreshWindowSize(int width, int height) { }
        public void SetTopMost(bool enable) { }
        public void SetTransparent(bool enable) { }
        public void SetWindowAlpha(byte alpha) { }
        public void SetActive() { }

        public NativeMethods.RECT GetPrimaryWindowRect() => new NativeMethods.RECT
        {
            left = 0,
            top = 0,
            right = Screen.currentResolution.width,
            bottom = Screen.currentResolution.height,
        };

        public List<NativeMethods.RECT> LoadAllMonitorRects() => new()
        {
            GetPrimaryWindowRect()
        };

        public Vector2Int GetMousePosition() => Vector2Int.RoundToInt(Input.mousePosition);
    }
}
