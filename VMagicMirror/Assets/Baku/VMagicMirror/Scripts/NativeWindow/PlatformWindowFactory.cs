using UnityEngine;

namespace Baku.VMagicMirror
{
    public static class PlatformWindowFactory
    {
        public static IPlatformWindow Create()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ||
                   Application.platform == RuntimePlatform.WindowsPlayer
                ? new WindowsPlatformWindow()
                : new LinuxPlatformWindow();
        }
    }
}
