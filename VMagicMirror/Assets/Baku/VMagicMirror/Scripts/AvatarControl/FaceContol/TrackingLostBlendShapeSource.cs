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
    public class TrackingLostBlendShapeSource : IInitializable, ITickable, IDisposable
    {
        // TODO:
        // - このクラスをBlendShapeResultSetterから読み取り「FaceSwitchに準ずるがFaceSwitchよりは優先」という位置づけで適用する

        private const float TrackingLostThreshold = 0.5f;
        private readonly IMessageReceiver _receiver;
        private readonly FaceControlConfiguration _config;

        private readonly MediaPipeFacialValueRepository _mediaPipeFacialValueRepository;
        private readonly ExternalTrackerDataSource _externalTrackerDataSource;
        
        private readonly CompositeDisposable _disposable = new();

        // 「トラッキングロス時にブレンドシェイプを利かす」という処理自体が有効かどうかのフラグ
        private readonly ReactiveProperty<bool> _isFeatureEnabled = new(false);
        private readonly ReactiveProperty<string> _cameraDeviceName = new("");

        private bool _isActive;
        private float _trackingLostTime;
        // とりあえずBlinkで決め打つが、GUIで選ばせるように拡張してもよいつもり
        // public ExpressionKey ExpressionKey { get; } = ExpressionKey.Blink;
        private readonly ReactiveProperty<ExpressionKey?> _expressionKey = new(null);
        public ReadOnlyReactiveProperty<ExpressionKey?> ExpressionKey => _expressionKey;
        
        [Inject]
        public TrackingLostBlendShapeSource(
            IMessageReceiver receiver,
            FaceControlConfiguration config,
            MediaPipeFacialValueRepository mediaPipeFacialValueRepository,
            ExternalTrackerDataSource externalTrackerDataSource)
        {
            _receiver = receiver;
            _config = config;
            _mediaPipeFacialValueRepository = mediaPipeFacialValueRepository;
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

        void IInitializable.Initialize()
        {
            _receiver.BindBoolProperty(VmmCommands.EnableFaceTrackingLostBlendShape, _isFeatureEnabled);
            _receiver.BindStringProperty(VmmCommands.SetCameraDeviceName, _cameraDeviceName);

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
                .AddTo(_disposable);
        }

        void ITickable.Tick()
        {
            if (!_isActive)
            {
                _trackingLostTime = 0f;
                _expressionKey.Value = null;
                return;
            }
            
            var tracked = _config.HeadMotionControlMode.CurrentValue is FaceControlModes.ExternalTracker
                ? _externalTrackerDataSource.Connected 
                : _mediaPipeFacialValueRepository.IsTracked;

            if (tracked)
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
        
        void IDisposable.Dispose() => _disposable.Dispose();
    }
}


