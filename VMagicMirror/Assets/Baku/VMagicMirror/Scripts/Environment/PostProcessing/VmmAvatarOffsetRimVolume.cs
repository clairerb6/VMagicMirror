using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Avatar Offset Rim")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Avatar Offset Rim")]
    public sealed class VmmAvatarOffsetRimVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        public BoolParameter useEmissiveBlend = new(false);
        public Vector2Parameter offset = new(Vector2.zero);
        public ColorParameter rimColor = new(Color.white, true, false, true);
        public ClampedFloatParameter applyRate = new(1f, 0f, 1f);
    }
}
