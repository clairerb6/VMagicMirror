using System;
using UnityEngine;
using Zenject;

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
    public sealed class ArmMuscleInterpolator : IInitializable
    {
        public const float DefaultInterpolationRate = 0.1f;
        public const bool IncludeFingerMuscles = false;

        private static readonly int[] _armMuscleIndices =
        {
            37, 38, 39, 40, 41, 42, 43, 44, 45,
            46, 47, 48, 49, 50, 51, 52, 53, 54,
        };

        private static readonly int[] _fingerMuscleIndices =
        {
            55, 56, 57, 58, 59, 60, 61, 62, 63, 64,
            65, 66, 67, 68, 69, 70, 71, 72, 73, 74,
            75, 76, 77, 78, 79, 80, 81, 82, 83, 84,
            85, 86, 87, 88, 89, 90, 91, 92, 93, 94,
        };

        private readonly IVRMLoadable _vrmLoadable;
        private readonly float[] _previousArmMuscles = new float[InterpolatedMuscleCount];
        private HumanPoseHandler _humanPoseHandler;
        private Transform _hips;
        private HumanPose _humanPose;
        private bool _hasModel;
        private bool _hasPreviousFramePose;

        private static int InterpolatedMuscleCount =>
            _armMuscleIndices.Length + (IncludeFingerMuscles ? _fingerMuscleIndices.Length : 0);

        [Inject]
        public ArmMuscleInterpolator(IVRMLoadable vrmLoadable)
        {
            _vrmLoadable = vrmLoadable;
        }

        public void Initialize()
        {
            _vrmLoadable.VrmLoaded += OnVrmLoaded;
            _vrmLoadable.VrmDisposing += OnVrmDisposing;
        }
        
        public bool HasModel => _hasModel;

        /// <summary>
        /// 前フレーム値と現在値を補間するときの比率です。0に近いほど前フレーム寄り、1で補間なしになります。
        /// </summary>
        public float InterpolationRate { get; set; } = DefaultInterpolationRate;

        /// <summary>
        /// 現在のポーズに対して腕のmuscle補間を適用します。
        /// </summary>
        /// <returns>補間を書き戻した場合はtrue</returns>
        public bool Interpolate() => Interpolate(InterpolationRate);

        /// <summary>
        /// 現在のポーズに対して腕のmuscle補間を適用します。
        /// </summary>
        /// <param name="interpolationRate">0に近いほど前フレーム寄り、1で補間なし</param>
        /// <returns>補間を書き戻した場合はtrue</returns>
        public bool Interpolate(float interpolationRate)
        {
            if (!_hasModel || _humanPoseHandler == null)
            {
                return false;
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

            if (!_hasPreviousFramePose)
            {
                CacheCurrentArmMuscles();
                _hasPreviousFramePose = true;
                return false;
            }

            float rate = Mathf.Clamp01(interpolationRate);
            InterpolateMuscles(_armMuscleIndices, ref rate, 0);
            if (IncludeFingerMuscles)
            {
                InterpolateMuscles(_fingerMuscleIndices, ref rate, _armMuscleIndices.Length);
            }

            _humanPoseHandler.SetHumanPose(ref _humanPose);
            if (hasHips)
            {
                _hips.localPosition = hipsLocalPosition;
                _hips.localRotation = hipsLocalRotation;
            }
            CacheCurrentArmMuscles();
            return true;
        }

        /// <summary>
        /// 次回の <see cref="Interpolate()"/> を初回扱いに戻します。
        /// </summary>
        public void Reset()
        {
            _hasPreviousFramePose = false;
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
            CacheCurrentArmMuscles();
            _hasPreviousFramePose = true;
            _hasModel = true;
        }

        private void OnVrmDisposing()
        {
            _hasModel = false;
            _hasPreviousFramePose = false;
            _hips = null;
            _humanPoseHandler?.Dispose();
            _humanPoseHandler = null;
            _humanPose = default;
        }

        private void CacheCurrentArmMuscles()
        {
            CacheMuscles(_armMuscleIndices, 0);
            if (IncludeFingerMuscles)
            {
                CacheMuscles(_fingerMuscleIndices, _armMuscleIndices.Length);
            }
        }

        private void InterpolateMuscles(int[] muscleIndices, ref float rate, int offset)
        {
            for (int i = 0; i < muscleIndices.Length; i++)
            {
                int muscleIndex = muscleIndices[i];
                _humanPose.muscles[muscleIndex] = Mathf.Lerp(
                    _previousArmMuscles[offset + i],
                    _humanPose.muscles[muscleIndex],
                    rate
                );
            }
        }

        private void CacheMuscles(int[] muscleIndices, int offset)
        {
            for (int i = 0; i < muscleIndices.Length; i++)
            {
                _previousArmMuscles[offset + i] = _humanPose.muscles[muscleIndices[i]];
            }
        }
    }
}
