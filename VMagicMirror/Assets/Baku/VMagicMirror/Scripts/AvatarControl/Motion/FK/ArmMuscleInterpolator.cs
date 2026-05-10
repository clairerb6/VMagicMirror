using UnityEngine;
using Zenject;
using R3;

namespace Baku.VMagicMirror.FK
{
    /// <summary>
    /// Humanoidの両腕まわりのmuscleを前フレーム値と補間し、ポーズを滑らかにするクラス
    /// </summary>
    /// <remarks>
    /// - 37-45: left shoulder / arm / forearm / hand
    /// - 46-54: right shoulder / arm / forearm / hand
    /// </remarks>
    public sealed class ArmMuscleInterpolator : PresenterBase
    {
        private const int MuscleCount = 95;
        private const float FilterSamplingRate = 60f;
        private const float FilterCutOffFrequency = 4f;

        private static readonly int[] ArmMuscleIndices =
        {
            37, 38, 39, 40, 41, 42, 43, 44, 45,
            46, 47, 48, 49, 50, 51, 52, 53, 54,
        };

        private readonly IVRMLoadable _vrmLoadable;
        private readonly CurrentFramerateChecker _framerateChecker;
        private readonly BiQuadFilter[] _muscleFilters = new BiQuadFilter[MuscleCount];
        private HumanPoseHandler _humanPoseHandler;
        private Transform _hips;
        private HumanPose _humanPose;
        private bool _hasModel;

        [Inject]
        public ArmMuscleInterpolator(
            IVRMLoadable vrmLoadable,
            CurrentFramerateChecker framerateChecker)
        {
            _vrmLoadable = vrmLoadable;
            _framerateChecker = framerateChecker;

            var referenceFilter = new BiQuadFilter();
            referenceFilter.SetUpAsLowPassFilter(FilterSamplingRate, FilterCutOffFrequency);
            _muscleFilters[0] = referenceFilter;
            for (int i = 1; i < _muscleFilters.Length; i++)
            {
                _muscleFilters[i] = new BiQuadFilter();
                _muscleFilters[i].CopyParametersFrom(referenceFilter);
            }
        }

        public override void Initialize()
        {
            _vrmLoadable.VrmLoaded += OnVrmLoaded;
            _vrmLoadable.VrmDisposing += OnVrmDisposing;
            
            _framerateChecker.CurrentFramerate
                .Subscribe(SetupFilterFrameRate)
                .AddTo(this);
        }
        
        private void SetupFilterFrameRate(float frameRate)
        {
            var samplingRate = Mathf.Max(frameRate, 1f);
            foreach (var filter in _muscleFilters)
            {
                filter.SetUpAsLowPassFilter(samplingRate, FilterCutOffFrequency);
            }
        }
        
        /// <summary> 現在のポーズに対して腕のmuscle補間を適用します。 </summary>
        public void Interpolate()
        {
            if (!_hasModel || _humanPoseHandler == null)
            {
                return;
            }

            Vector3 hipsLocalPosition = Vector3.zero;
            Quaternion hipsLocalRotation = Quaternion.identity;
            bool hasHips = _hips != null;
            if (hasHips)
            {
                hipsLocalPosition = _hips.localPosition;
                hipsLocalRotation = _hips.localRotation;
            }

            _humanPoseHandler.GetHumanPose(ref _humanPose);
            ApplyFilters(ArmMuscleIndices);

            _humanPoseHandler.SetHumanPose(ref _humanPose);
            if (hasHips)
            {
                _hips.localPosition = hipsLocalPosition;
                _hips.localRotation = hipsLocalRotation;
            }
        }

        /// <summary>
        /// フィルタの内部状態を現在のpose値に揃えます。
        /// </summary>
        public void Reset()
        {
            if (!_hasModel || _humanPoseHandler == null)
            {
                return;
            }

            _humanPoseHandler.GetHumanPose(ref _humanPose);
            ResetFiltersToCurrentPose();
        }

        private void OnVrmLoaded(VrmLoadedInfo info)
        {
            OnVrmDisposing();

            if (info.animator == null || info.animator.avatar == null || !info.animator.avatar.isHuman)
            {
                return;
            }

            _humanPoseHandler = new HumanPoseHandler(info.animator.avatar, info.animator.transform);
            _hips = info.controlRig.GetBoneTransform(HumanBodyBones.Hips);
            _humanPoseHandler.GetHumanPose(ref _humanPose);
            ResetFiltersToCurrentPose();
            _hasModel = true;
        }

        private void OnVrmDisposing()
        {
            _hasModel = false;
            _hips = null;
            _humanPoseHandler?.Dispose();
            _humanPoseHandler = null;
            _humanPose = default;
        }

        private void ApplyFilters(int[] muscleIndices)
        {
            for (int i = 0; i < muscleIndices.Length; i++)
            {
                int muscleIndex = muscleIndices[i];
                _humanPose.muscles[muscleIndex] = _muscleFilters[muscleIndex].Update(_humanPose.muscles[muscleIndex]);
            }
        }

        private void ResetFiltersToCurrentPose()
        {
            ResetFilters(ArmMuscleIndices);
        }

        private void ResetFilters(int[] muscleIndices)
        {
            for (int i = 0; i < muscleIndices.Length; i++)
            {
                int muscleIndex = muscleIndices[i];
                _muscleFilters[muscleIndex].ResetValue(_humanPose.muscles[muscleIndex]);
            }
        }
    }
}
