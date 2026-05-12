using UnityEngine;
using Zenject;
using R3;

namespace Baku.VMagicMirror.FK
{
    /// <summary>
    /// Humanoidの両腕まわりのmuscleを前フレーム値と補間し、ポーズを滑らかにするクラス。
    /// ハンドトラッキング中の腕の動きのブラッシュアップ用に使う
    /// </summary>
    /// <remarks>
    /// - 37-45: left shoulder / arm / forearm / hand
    /// - 46-54: right shoulder / arm / forearm / hand
    /// </remarks>
    public sealed class ArmMuscleInterpolator : PresenterBase
    {
        private const float FilterSamplingRate = 60f;
        private const float FilterCutOffFrequency = 4f;

        private static readonly int[] LeftArmMuscleIndices = { 37, 38, 39, 40, 41, 42, 43, 44, 45 };
        private static readonly int[] RightArmMuscleIndices = { 46, 47, 48, 49, 50, 51, 52, 53, 54 };

        private readonly IVRMLoadable _vrmLoadable;
        private readonly HandIKIntegrator _handIKIntegrator;
        private readonly CurrentFramerateChecker _framerateChecker;
        private readonly (int index, BiQuadFilter filter)[] _leftArmMuscleFilters;
        private readonly (int index, BiQuadFilter filter)[] _rightArmMuscleFilters;
        private HumanPoseHandler _humanPoseHandler;
        private Transform _hips;
        private HumanPose _humanPose;
        private bool _hasModel;

        [Inject]
        public ArmMuscleInterpolator(
            IVRMLoadable vrmLoadable,
            HandIKIntegrator handIKIntegrator,
            CurrentFramerateChecker framerateChecker)
        {
            _vrmLoadable = vrmLoadable;
            _handIKIntegrator = handIKIntegrator;
            _framerateChecker = framerateChecker;

            var referenceFilter = new BiQuadFilter();
            referenceFilter.SetUpAsLowPassFilter(FilterSamplingRate, FilterCutOffFrequency);

            _leftArmMuscleFilters = new (int index, BiQuadFilter filter)[LeftArmMuscleIndices.Length];
            for (var i = 0; i < LeftArmMuscleIndices.Length; i++)
            {
                var filter = new BiQuadFilter();
                filter.CopyParametersFrom(referenceFilter);
                _leftArmMuscleFilters[i] = (LeftArmMuscleIndices[i], filter);
            }
            
            _rightArmMuscleFilters = new (int index, BiQuadFilter filter)[RightArmMuscleIndices.Length];
            for (var i = 0; i < RightArmMuscleIndices.Length; i++)
            {
                var filter = new BiQuadFilter();
                filter.CopyParametersFrom(referenceFilter);
                _rightArmMuscleFilters[i] = (RightArmMuscleIndices[i], filter);
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
            foreach (var pair in _leftArmMuscleFilters)
            {
                pair.filter.SetUpAsLowPassFilter(samplingRate, FilterCutOffFrequency);
            }
            foreach (var pair in _rightArmMuscleFilters)
            {
                pair.filter.SetUpAsLowPassFilter(samplingRate, FilterCutOffFrequency);
            }
        }
        
        public void Update()
        {
            if (!_hasModel || _humanPoseHandler == null)
            {
                return;
            }

            var updateLeft = _handIKIntegrator.LeftTargetType.CurrentValue is HandTargetType.ImageBaseHand;
            var updateRight = _handIKIntegrator.RightTargetType.CurrentValue is HandTargetType.ImageBaseHand;

            if (!updateLeft && !updateRight)
            {
                Reset();
                return;
            }

            // hipsは書き戻さないとズレることがあるようなので明示的にキャッシュする
            var hipsLocalPosition = _hips.localPosition;
            var hipsLocalRotation = _hips.localRotation;

            _humanPoseHandler.GetHumanPose(ref _humanPose);
            if (updateLeft)
            {
                ApplyFilters(_leftArmMuscleFilters);
            }
            else
            {
                ResetFilters(_leftArmMuscleFilters);
            }

            if (updateRight)
            {
                ApplyFilters(_rightArmMuscleFilters);
            }
            else
            {
                ResetFilters(_rightArmMuscleFilters);
            }

            _humanPoseHandler.SetHumanPose(ref _humanPose);
            _hips.localPosition = hipsLocalPosition;
            _hips.localRotation = hipsLocalRotation;
        }

        /// <summary>
        /// フィルタの内部状態を現在のpose値に揃えます。
        /// </summary>
        private void Reset()
        {
            if (!_hasModel || _humanPoseHandler == null)
            {
                return;
            }

            _humanPoseHandler.GetHumanPose(ref _humanPose);
            ResetBothArmFilters();
        }

        private void OnVrmLoaded(VrmLoadedInfo info)
        {
            OnVrmDisposing();

            _humanPoseHandler = new HumanPoseHandler(info.animator.avatar, info.animator.transform);
            _hips = info.animator.GetBoneTransform(HumanBodyBones.Hips);
            _humanPoseHandler.GetHumanPose(ref _humanPose);
            ResetBothArmFilters();
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

        private void ApplyFilters((int index, BiQuadFilter filter)[] filters)
        {
            foreach (var pair in filters)
            {
                _humanPose.muscles[pair.index] = pair.filter.Update(_humanPose.muscles[pair.index]);
            }
        }

        private void ResetBothArmFilters()
        {
            ResetFilters(_leftArmMuscleFilters);
            ResetFilters(_rightArmMuscleFilters);
        }

        private void ResetFilters((int index, BiQuadFilter filter)[] filters)
        {
            foreach (var pair in filters)
            {
                pair.filter.ResetValue(_humanPose.muscles[pair.index]);
            }
        }
    }
}
