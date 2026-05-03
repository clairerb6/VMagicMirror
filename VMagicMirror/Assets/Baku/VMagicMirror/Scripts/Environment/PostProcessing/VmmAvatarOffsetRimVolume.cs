using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Crop")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Avatar Offset Rim")]
    public sealed class VmmAvatarOffsetRimVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        public ColorParameter rimColor = new(Color.white, false, false, true);
        public ClampedFloatParameter applyRate = new(1f, 0f, 1f);
    }
}
