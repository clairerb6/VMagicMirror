using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Baku.VMagicMirror
{
    public static class VmmVolumeComponentAccessor
    {
        private static Volume _globalVolume;

        public static bool HasAnyActiveEffect() =>
            HasAnyPreBloomEffect() ||
            HasAnyPostProcessEffect();

        public static bool HasAnyPreBloomEffect() =>
            GetAvatarOffsetRimVolumeFromStack().enabled.value;

        public static bool HasAnyPostProcessEffect() =>
            GetCropVolumeFromStack().enabled.value ||
            GetAlphaEdgeVolumeFromStack().enabled.value ||
            GetRetroVolumeFromStack().enabled.value ||
            GetColoredSsaoVolumeFromStack().enabled.value;

        public static VmmAvatarOffsetRimVolume GetAvatarOffsetRimVolumeFromStack() =>
            GetComponentFromStack<VmmAvatarOffsetRimVolume>();
        
        public static VmmCropVolume GetCropVolumeFromStack() =>
            GetComponentFromStack<VmmCropVolume>();

        public static VmmAlphaEdgeVolume GetAlphaEdgeVolumeFromStack() =>
            GetComponentFromStack<VmmAlphaEdgeVolume>();

        public static VmmRetroVolume GetRetroVolumeFromStack() =>
            GetComponentFromStack<VmmRetroVolume>();

        public static VmmColoredSsaoVolume GetColoredSsaoVolumeFromStack() =>
            GetComponentFromStack<VmmColoredSsaoVolume>();

        public static void UpdateCrop(Action<VmmCropVolume> updateAction)
        {
            if (TryGetOrCreateRuntimeComponent(out VmmCropVolume component))
            {
                updateAction(component);
            }
        }

        public static void UpdateAvatarOffsetRim(Action<VmmAvatarOffsetRimVolume> updateAction)
        {
            if (TryGetOrCreateRuntimeComponent(out VmmAvatarOffsetRimVolume component))
            {
                updateAction(component);
            }
        }

        public static void SetVmmAvatarOffsetRimActive(bool active)
        {
            UpdateAvatarOffsetRim(component => component.enabled.Override(active));
        }

        public static void SetVmmCropActive(bool active)
        {
            UpdateCrop(component => component.enabled.Override(active));
        }

        public static void UpdateAlphaEdge(Action<VmmAlphaEdgeVolume> updateAction)
        {
            if (TryGetOrCreateRuntimeComponent(out VmmAlphaEdgeVolume component))
            {
                updateAction(component);
            }
        }

        public static void SetVmmAlphaEdgeActive(bool active)
        {
            UpdateAlphaEdge(component => component.enabled.Override(active));
        }

        public static void UpdateRetro(Action<VmmRetroVolume> updateAction)
        {
            if (TryGetOrCreateRuntimeComponent(out VmmRetroVolume component))
            {
                updateAction(component);
            }
        }

        public static void SetVmmRetroActive(bool active)
        {
            UpdateRetro(component => component.enabled.Override(active));
        }

        public static void UpdateColoredSsao(Action<VmmColoredSsaoVolume> updateAction)
        {
            if (TryGetOrCreateRuntimeComponent(out VmmColoredSsaoVolume component))
            {
                updateAction(component);
            }
        }

        public static void SetVmmColoredSsaoActive(bool active)
        {
            UpdateColoredSsao(component => component.enabled.Override(active));
        }

        private static T GetComponentFromStack<T>() where T : VolumeComponent
        {
            var component = VolumeManager.instance.stack.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"Volume stack does not contain {typeof(T).Name}.");
            }

            return component;
        }

        private static bool TryGetOrCreateRuntimeComponent<T>(out T component) where T : VolumeComponent
        {
            component = null;
            var volume = GetOrFindGlobalVolume();
            if (volume == null)
            {
                return false;
            }

            var profile = volume.profile != null ? volume.profile : volume.sharedProfile;
            if (profile == null)
            {
                return false;
            }

            if (volume.profile == null && volume.sharedProfile != null)
            {
                volume.profile = UnityEngine.Object.Instantiate(volume.sharedProfile);
                profile = volume.profile;
            }

            if (!profile.TryGet(out component))
            {
                component = profile.Add<T>(true);
            }

            return component != null;
        }

        private static Volume GetOrFindGlobalVolume()
        {
            if (_globalVolume != null)
            {
                return _globalVolume;
            }

            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var volume in volumes)
            {
                if (!volume.isGlobal)
                {
                    continue;
                }

                var profile = volume.profile != null ? volume.profile : volume.sharedProfile;
                if (profile == null)
                {
                    continue;
                }

                _globalVolume = volume;
                return volume;
            }

            return null;
        }
    }
}
