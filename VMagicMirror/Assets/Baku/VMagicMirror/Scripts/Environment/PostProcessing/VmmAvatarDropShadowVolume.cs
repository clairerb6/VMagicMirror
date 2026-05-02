using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("VMagicMirror/Avatar Drop Shadow")]
    public sealed class VmmAvatarDropShadowVolume : VolumeComponent, IPostProcessComponent
    {
        public Vector2Parameter offset = new(Vector2.zero);
        public Vector2Parameter scale = new(Vector2.one);
        public ColorParameter color = new(new Color(0f, 0f, 0f, 0.35f));
        public ClampedFloatParameter alphaThreshold = new(0.001f, 0f, 1f);

        public bool IsActive()
        {
            var scaleDelta = scale.value - Vector2.one;
            return active &&
                color.value.a > 0.001f &&
                (offset.value.sqrMagnitude > 0.0f || scaleDelta.sqrMagnitude > 0.0f);
        }

        public bool IsTileCompatible() => false;

        public static VmmAvatarDropShadowVolume GetActiveComponent()
        {
            var stack = VolumeManager.instance?.stack;
            if (stack == null)
            {
                return null;
            }

            var component = stack.GetComponent<VmmAvatarDropShadowVolume>();
            return component != null && component.IsActive() ? component : null;
        }
    }
}
