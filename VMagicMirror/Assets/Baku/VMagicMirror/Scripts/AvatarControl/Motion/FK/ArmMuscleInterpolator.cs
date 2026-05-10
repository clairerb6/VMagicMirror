using UnityEngine;
using Zenject;
using R3;

namespace Baku.VMagicMirror.FK
{
    /// <summary>
    /// Humanoidの両腕まわりのmuscleを前フレーム値と補間し、ポーズを滑らかにするクラス
    /// </summary>
    /// <remarks>
    /// arm系のmuscle indexは下記を対象にする。
    /// ref: https://gist.github.com/neon-izm/0637dac7a29682de916cecc0e8b037b0
    /// - 37-45: left shoulder / arm / forearm / hand
    /// - 46-54: right shoulder / arm / forearm / hand
    /// </remarks>
    public sealed class ArmMuscleInterpolator : PresenterBase
    {
        public const bool IncludeFingerMuscles = false;
        private const int MuscleCount = 95;
        private const float FilterSamplingRate = 60f;
        private const float FilterCutOffFrequency = 4f;

        private static readonly int[] ArmMuscleIndices =
        {
            37, 38, 39, 40, 41, 42, 43, 44, 45,
            46, 47, 48, 49, 50, 51, 52, 53, 54,
        };

        private static readonly int[] FingerMuscleIndices =
        {
            55, 56, 57, 58, 59, 60, 61, 62, 63, 64,
            65, 66, 67, 68, 69, 70, 71, 72, 73, 74,
            75, 76, 77, 78, 79, 80, 81, 82, 83, 84,
            85, 86, 87, 88, 89, 90, 91, 92, 93, 94,
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
        
        public bool HasModel => _hasModel;

        private void SetupFilterFrameRate(float frameRate)
        {
            var samplingRate = Mathf.Max(frameRate, 1f);
            foreach (var filter in _muscleFilters)
            {
                filter.SetUpAsLowPassFilter(samplingRate, FilterCutOffFrequency);
            }
        }
        
        /// <summary>
        /// 現在のポーズに対して腕のmuscle補間を適用します。
        /// </summary>
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
            if (IncludeFingerMuscles)
            {
                ApplyFilters(FingerMuscleIndices);
            }

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
            if (IncludeFingerMuscles)
            {
                ResetFilters(FingerMuscleIndices);
            }
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
