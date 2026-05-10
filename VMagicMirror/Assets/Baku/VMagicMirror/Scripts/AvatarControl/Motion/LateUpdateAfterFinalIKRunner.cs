using Baku.VMagicMirror.FK;
using R3;
using UnityEngine;

namespace Baku.VMagicMirror
{
    public sealed class LateUpdateAfterFinalIKRunner : PresenterBase
    {
        private readonly LateUpdateSourceAfterFinalIK _source;

        private readonly MediaPipeHandLocalRotLimiter _mediaPipeHandLocalRotLimiter;
        private readonly VrmaMotionSetter _vrmaMotionSetter;
        private readonly ArmMuscleInterpolator _armMuscleInterpolator;

        public LateUpdateAfterFinalIKRunner(
            LateUpdateSourceAfterFinalIK source,
            MediaPipeHandLocalRotLimiter mediaPipeHandLocalRotLimiter,
            VrmaMotionSetter vrmaMotionSetter,
            ArmMuscleInterpolator armMuscleInterpolator
            )
        {
            _source = source;
            _mediaPipeHandLocalRotLimiter = mediaPipeHandLocalRotLimiter;
            _vrmaMotionSetter = vrmaMotionSetter;
            _armMuscleInterpolator = armMuscleInterpolator;
        }
        
        public override void Initialize()
        {
            _source.OnLateUpdate
                .Subscribe(_ => OnLateUpdate())
                .AddTo(this);
        }

        private void OnLateUpdate()
        {
            _mediaPipeHandLocalRotLimiter.LateUpdate();
            _vrmaMotionSetter.ApplyUpdate();

            // DEBUG: オンオフして見比べる用
            var qualityLevel = QualitySettings.GetQualityLevel();
            if (qualityLevel == 5)
            {
                _armMuscleInterpolator.Interpolate(0.1f);
            }
            else if (qualityLevel == 4)
            {
                _armMuscleInterpolator.Interpolate(0.3f);
            }
            else
            {
                _armMuscleInterpolator.Reset();
            }
        }
    }
}
