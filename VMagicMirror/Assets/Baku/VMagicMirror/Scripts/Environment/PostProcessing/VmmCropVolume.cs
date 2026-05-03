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
}
