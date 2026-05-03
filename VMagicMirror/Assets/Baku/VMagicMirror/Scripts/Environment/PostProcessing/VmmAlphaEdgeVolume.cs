using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Alpha Edge")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Alpha Edge")]
    public sealed class VmmAlphaEdgeVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        public FloatParameter thickness = new(20f);
        public FloatParameter threshold = new(1f);
        public ColorParameter edgeColor = new(Color.white, false, false, true);
        public FloatParameter outlineOverwriteAlpha = new(0.02f);
        public BoolParameter highQualityMode = new(false);
    }
}
