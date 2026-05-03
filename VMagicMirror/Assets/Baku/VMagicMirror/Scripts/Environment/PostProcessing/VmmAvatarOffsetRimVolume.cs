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
        // NOTE: ほんとはoffsetを保持したほうがいいが、絵面のチェック用に一時的に極座標系を導入している
        public Vector2Parameter offset = new(Vector2.zero);
        public ClampedFloatParameter offsetAngle = new(0f, 0f, 360f);
        public ClampedFloatParameter offsetMilliMagnitude = new(0f, 0f, 10f);
        public ColorParameter rimColor = new(Color.white, false, false, true);
        public ClampedFloatParameter applyRate = new(1f, 0f, 1f);
    }
}
