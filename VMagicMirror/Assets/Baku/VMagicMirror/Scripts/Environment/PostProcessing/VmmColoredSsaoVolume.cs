using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Colored SSAO")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Colored SSAO")]
    public sealed class VmmColoredSsaoVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
        public ColorParameter color = new(Color.black, false, false, true);
    }
}
