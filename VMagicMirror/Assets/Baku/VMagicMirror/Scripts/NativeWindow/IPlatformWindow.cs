using System.Collections.Generic;
using UnityEngine;

namespace Baku.VMagicMirror
{
    public interface IPlatformWindow
    {
        bool SupportsNativeWindowControl { get; }

        uint GetWindowStyle();
        uint GetExWindowStyle();
        void SetWindowStyle(uint style);
        void SetExWindowStyle(uint style);

        bool TryGetWindowRect(out NativeMethods.RECT rect);
        Vector2Int GetWindowPosition();
        void SetWindowPosition(int x, int y);
        void SetWindowSize(int width, int height);
        void RefreshWindowSize(int width, int height);

        void SetTopMost(bool enable);
        void SetTransparent(bool enable);
        void SetWindowAlpha(byte alpha);
        void SetActive();

        NativeMethods.RECT GetPrimaryWindowRect();
        List<NativeMethods.RECT> LoadAllMonitorRects();
        Vector2Int GetMousePosition();
    }
}
