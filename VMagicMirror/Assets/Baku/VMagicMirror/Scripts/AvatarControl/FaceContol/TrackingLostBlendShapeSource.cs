using System;
using Baku.VMagicMirror.ExternalTracker;
using Baku.VMagicMirror.MediaPipeTracker;
using R3;
using UnityEngine;
using UniVRM10;
using Zenject;

namespace Baku.VMagicMirror
{
    /// <summary>
    /// 顔トラッキングを行う設定であり、かつそのトラッキングがロストしているときに適用したいブレンドシェイプ情報を出力するようなクラス
    /// </summary>
    public class TrackingLostBlendShapeSource : PresenterBase, ITickable
    {
        private const float TrackingLostThreshold = 1.0f;
        
        // トラッキングがこの時間だけ成功し続けると _trackingSucceedAtLeastOnce を true にする。
        // なぜ1Fの判定じゃダメかというと、
        // 「顔が検出できてない状態でiFacialMocapが送ってくるデータ」をトラッキングロスト扱いする判定に時間がかかるから
        private const float TrackingFirstSucceedDuration = 1.0f;
        
        private readonly IMessageReceiver _receiver;
        private readonly FaceControlConfiguration _config;

        private readonly MediaPipeKinematicSetter _mediaPipeKinematicSetter;
        private readonly ExternalTrackerDataSource _externalTrackerDataSource;
        
        // 「トラッキングロス時にブレンドシェイプを利かす」という処理自体が有効かどうかのフラグ
        private readonly ReactiveProperty<bool> _isFeatureEnabled = new(false);
        private readonly ReactiveProperty<string> _cameraDeviceName = new("");

        private bool _isActive;
        private bool _trackingSucceedAtLeastOnce;
        private float _trackedTime;
        private float _trackingLostTime;

        // とりあえずBlinkで決め打つが、GUIで選ばせるように拡張してもよいつもり
        // public ExpressionKey ExpressionKey { get; } = ExpressionKey.Blink;
        private readonly ReactiveProperty<ExpressionKey?> _expressionKey = new(null);
        public ReadOnlyReactiveProperty<ExpressionKey?> ExpressionKey => _expressionKey;
        
        [Inject]
        public TrackingLostBlendShapeSource(
            IMessageReceiver receiver,
            FaceControlConfiguration config,
            MediaPipeKinematicSetter mediaPipeKinematicSetter,
            ExternalTrackerDataSource externalTrackerDataSource)
        {
            _receiver = receiver;
            _config = config;
            _mediaPipeKinematicSetter = mediaPipeKinematicSetter;
            _externalTrackerDataSource = externalTrackerDataSource;
        }

        public bool HasRequest => _expressionKey.CurrentValue.HasValue;

        public void Accumulate(ExpressionAccumulator accumulator, float weight = 1f)
        {
            if (_expressionKey.CurrentValue.HasValue)
            {
                accumulator.Accumulate(_expressionKey.CurrentValue.Value, weight);
            }
        }

        public override void Initialize()
        {
            _receiver.BindBoolProperty(VmmCommands.EnableFaceTrackingLostBlendShape, _isFeatureEnabled);
            _receiver.BindStringProperty(VmmCommands.SetCameraDeviceName, _cameraDeviceName);

            _config.HeadMotionControlMode
                .Subscribe(_ => _trackingSucceedAtLeastOnce = false)
                .AddTo(this);
            
            _isFeatureEnabled.CombineLatest(
                _config.HeadMotionControlMode,
                _cameraDeviceName, 
                    (enabled, mode, cameraName) => (enabled, mode, cameraName)
                )
                .DistinctUntilChanged()
                .Subscribe(value =>
                {
                    // NOTE: VMCProtocolの受信が絡むケースはトラッキングロスの概念が扱いにくいので考えないことにする
                    _isActive = value.enabled && value.mode switch
                    {
                        FaceControlModes.ExternalTracker => true,
                        FaceControlModes.WebCamHighPower or FaceControlModes.WebCamLowPower
                            => !string.IsNullOrEmpty(value.cameraName),
                        _ => false,
                    };
                    
                })
                .AddTo(this);
        }

        void ITickable.Tick()
        {
            if (!_isActive)
            {
                _trackingLostTime = 0f;
                _trackingSucceedAtLeastOnce = false;
                _expressionKey.Value = null;
                return;
            }
            
            var tracked = _config.HeadMotionControlMode.CurrentValue is FaceControlModes.ExternalTracker
                ? _externalTrackerDataSource.Connected 
                : _mediaPipeKinematicSetter.TryGetHeadPose(out _);

            if (tracked && !_trackingSucceedAtLeastOnce)
            {
                _trackedTime += Time.deltaTime;
                if (_trackedTime >= TrackingFirstSucceedDuration)
                {
                    _trackingSucceedAtLeastOnce = true;
                }
            }

            if (!tracked)
            {
                _trackedTime = 0f;
            }
            
            // トラッキングに1回成功するまではトラッキングロストしててもBlendShapeが作動しない
            if (tracked || !_trackingSucceedAtLeastOnce)
            {
                _trackingLostTime = 0f;
                _expressionKey.Value = null;
                return;
            }
            
            _trackingLostTime += Time.deltaTime;
            if (_trackingLostTime >= TrackingLostThreshold)
            {
                _expressionKey.Value = UniVRM10.ExpressionKey.Blink;
            }
        }
    }
}


