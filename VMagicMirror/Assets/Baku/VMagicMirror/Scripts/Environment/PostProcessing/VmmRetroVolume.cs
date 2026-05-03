using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    [Serializable]
    [VolumeComponentMenu("Post-processing Custom/VMM Retro")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "VMM Retro")]
    public sealed class VmmRetroVolume : VolumeComponent
    {
        public BoolParameter enabled = new(false);
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

}
