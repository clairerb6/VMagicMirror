using Baku.VMagicMirror.FK;
using R3;
using Zenject;

namespace Baku.VMagicMirror
{
    public sealed class LateUpdateAfterFinalIKRunner : PresenterBase
    {
        private readonly LateUpdateSourceAfterFinalIK _source;

        private readonly MediaPipeHandLocalRotLimiter _mediaPipeHandLocalRotLimiter;
        private readonly VrmaMotionSetter _vrmaMotionSetter;
        private readonly ArmMuscleInterpolator _armMuscleInterpolator;

        [Inject]
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
            _armMuscleInterpolator.Update();
        }
    }
}
