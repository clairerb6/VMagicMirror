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
        public FloatParameter thickness = new(15f);
        public FloatParameter threshold = new(0.5f);
        public ColorParameter edgeColor = new(Color.white, true, false, true);
        public FloatParameter outlineOverwriteAlpha = new(0.8f);
        public BoolParameter highQualityMode = new(false);
    }
}
