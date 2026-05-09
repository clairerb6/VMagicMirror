using System;
using R3;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace Baku.VMagicMirror
{
    public class ImageQualitySettingReceiver : PresenterBase
    {
        private const string DefaultQualityName = "High";
        // デフォルトのHigh品質以上ではデフォルトではHDRを有効とする
        private const int HdrEnabledMinimumQualityIndex = 3;

        private readonly IMessageReceiver _receiver;
        
        private readonly ReactiveProperty<string> _currentQualityName = new(DefaultQualityName);
        private readonly ReactiveProperty<bool> _disableHdrAlways = new(false);
        private readonly ReactiveProperty<bool> _windowFrameVisible = new(true);
        private readonly ReactiveProperty<int> _targetFramerate = new(60);

        [Inject]
        public ImageQualitySettingReceiver(IMessageReceiver receiver)
        {
            _receiver = receiver;
        }

        public override void Initialize()
        {
            _receiver.BindStringProperty(VmmCommands.SetImageQuality, _currentQualityName);
            _receiver.BindBoolProperty(VmmCommands.SetDisableHdrAlways, _disableHdrAlways);
            _receiver.BindBoolProperty(VmmCommands.WindowFrameVisibility, _windowFrameVisible);

            _receiver.BindIntProperty(VmmCommands.SetTargetFramerate, _targetFramerate);

            _receiver.AssignQueryHandler(
                VmmCommands.GetQualitySettingsInfo,
                q =>
                {
                    q.Result = JsonUtility.ToJson(new ImageQualityInfo()
                    {
                        ImageQualityNames = QualitySettings.names,
                        CurrentQualityIndex = QualitySettings.GetQualityLevel(),
                    });
                });
            
            _receiver.AssignQueryHandler(
                VmmCommands.ApplyDefaultImageQuality,
                q => { 
                    SetImageQuality(DefaultQualityName, _disableHdrAlways.Value, _windowFrameVisible.Value);
                    q.Result = DefaultQualityName;
                });

            _currentQualityName
                .CombineLatest(
                    _disableHdrAlways,
                    _windowFrameVisible,
                    (qualityName, disableHdrAlways, windowFrameVisible) =>
                    {
                        var enableHdr = false;
                        if (!disableHdrAlways)
                        {
                            var qualityIndex = FindIndexOfQuality(qualityName);
                            if (qualityIndex == -1)
                            {
                                return (qualityName: "", enableHdr, windowFrameVisible);
                            }

                            enableHdr = qualityIndex >= HdrEnabledMinimumQualityIndex;
                        }

                        return (qualityName, enableHdr, windowFrameVisible);
                    })
                .DistinctUntilChanged()
                .Subscribe(value => 
                    SetImageQuality(value.qualityName, value.enableHdr, value.windowFrameVisible))
                .AddTo(this);

            _targetFramerate
                .Subscribe(SetTargetFramerate)
                .AddTo(this);
        }

        private static void SetImageQuality(string name, bool hdrEnabled, bool windowFrameVisible)
        {
            if (!TrySetImageQuality(name))
            {
                return;
            }

            var urpAsset = (UniversalRenderPipelineAsset) QualitySettings.renderPipeline;
            urpAsset.supportsHDR = hdrEnabled;
            urpAsset.hdrColorBufferPrecision = hdrEnabled && !windowFrameVisible
                ? HDRColorBufferPrecision._64Bits
                : HDRColorBufferPrecision._32Bits;
        }
        
        private static bool TrySetImageQuality(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            var names = QualitySettings.names;
            //foreachにしてないのはIndexOfより手軽にパフォーマンス取れそうだから
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    return true;
                }
            }

            return false;
        }

        private static int FindIndexOfQuality(string name)
        {
            var names = QualitySettings.names;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void SetTargetFramerate(int value)
        {
            Debug.Log($"{nameof(SetTargetFramerate)}: {value}");
            // - FPSが0以下の場合、vSyncを有効化してモニターのリフレッシュレートに合わせる要求だと解釈する
            // - 30未満のFPSを指定された場合も異常値扱いし、vSyncが有効な状態に帰着させる
            if (value < 30)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = 30;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = value;
            }
        }
    }
    
    [Serializable]
    public class ImageQualityInfo
    {
        public string[] ImageQualityNames;
        public int CurrentQualityIndex;
    }
}
