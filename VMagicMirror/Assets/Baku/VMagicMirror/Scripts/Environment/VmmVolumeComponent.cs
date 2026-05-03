using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Crop")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Crop")]
    public sealed class VmmCropVolume : VolumeComponent
    {
        public FloatParameter margin = new(0.02f);
        public ClampedFloatParameter squareRate = new(0.0f, 0.0f, 1.0f);
        public FloatParameter borderWidth = new(0.01f);
        public ColorParameter borderColor = new(Color.white, false, false, true);
    }

    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Alpha Edge")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Alpha Edge")]
    public sealed class VmmAlphaEdgeVolume : VolumeComponent
    {
        public FloatParameter thickness = new(20f);
        public FloatParameter threshold = new(1f);
        public ColorParameter edgeColor = new(Color.white, false, false, true);
        public FloatParameter outlineOverwriteAlpha = new(0.02f);
        public BoolParameter highQualityMode = new(false);
    }

    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Retro")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Retro")]
    public sealed class VmmRetroVolume : VolumeComponent
    {
        public BoolParameter useBlock = new(false);
        public ClampedIntParameter blockSize = new(4, 2, 30);
        public BoolParameter useMonochrome = new(true);
        public ColorParameter black = new(new Color(0.16470589f, 0.14117648f, 0.08627451f), false, false, true);
        public ColorParameter white = new(new Color(0.9228164f, 0.941f, 0.7909855f), false, false, true);
        public BoolParameter useLevel = new(true);
        public ClampedIntParameter levelDivision = new(8, 2, 20);
        public ClampedIntParameter whiteThreshold = new(4, 1, 20);
        public BoolParameter useColorReduction = new(false);
        public ClampedIntParameter colorDivision = new(16, 4, 64);
        public ClampedFloatParameter bleeding = new(0.25f, 0f, 1f);
        public ClampedFloatParameter fringing = new(0.373f, 0f, 1f);
        public ClampedFloatParameter scanline = new(0.117f, 0f, 1f);
    }

    public static class VmmVolumeComponentAccessor
    {
        private static Volume _globalVolume;

        public static bool HasAnyActiveEffect() =>
            GetCropVolumeFromStack().active ||
            GetAlphaEdgeVolumeFromStack().active ||
            GetRetroVolumeFromStack().active;
        
        public static VmmCropVolume GetCropVolumeFromStack() =>
            GetComponentFromStack<VmmCropVolume>();

        public static VmmAlphaEdgeVolume GetAlphaEdgeVolumeFromStack() =>
            GetComponentFromStack<VmmAlphaEdgeVolume>();

        public static VmmRetroVolume GetRetroVolumeFromStack() =>
            GetComponentFromStack<VmmRetroVolume>();

        public static void UpdateCrop(Action<VmmCropVolume> updateAction)
        {
            if (TryGetOrCreateRuntimeComponent(out VmmCropVolume component))
            {
                updateAction(component);
            }
        }

        public static void SetVmmCropActive(bool active)
        {
            UpdateCrop(component => component.active = active);
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
            UpdateAlphaEdge(component => component.active = active);
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
            UpdateRetro(component => component.active = active);
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
