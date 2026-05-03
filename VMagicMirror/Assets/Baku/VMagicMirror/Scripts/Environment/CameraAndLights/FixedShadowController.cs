using Baku.VMagicMirror.VMCP;
using R3;
using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror
{
    public class FixedShadowController : PresenterBase
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly IMessageReceiver _receiver;
        private readonly BodyMotionModeController _bodyMotionModeController;
        private readonly VMCPReceiver _vmcpReceiver;
        private readonly Renderer _fixedShadowBoardRenderer;

        // NOTE: この5つはGUIから直接降ってくるやつ
        private readonly ReactiveProperty<bool> _shadowEnabled = new(true);
        private readonly ReactiveProperty<bool> _fixedShadowEnabledAlways = new(false);
        private readonly ReactiveProperty<bool> _fixedShadowEnabledWhenLocomotionActive = new(true);
        
        private readonly ReactiveProperty<bool> _fixedShadowEnabled = new(false);
        public ReactiveProperty<bool> FixedShadowEnabled => _fixedShadowEnabled;

        private readonly MaterialPropertyBlock _propertyBlock = new();
        private Color _shadowColor = new(0f, 0f, 0f, 0.6f);
        private float _shadowIntensity = 1.0f;
        
        [Inject]
        public FixedShadowController(
            IMessageReceiver receiver,
            BodyMotionModeController bodyMotionModeController,
            VMCPReceiver vmcpReceiver,
            Renderer fixedShadowBoardRenderer
            )
        {
            _receiver = receiver;
            _bodyMotionModeController = bodyMotionModeController;
            _vmcpReceiver = vmcpReceiver;

            _fixedShadowBoardRenderer = fixedShadowBoardRenderer;
        }
        
        public override void Initialize()
        {
            _receiver.BindBoolProperty(VmmCommands.ShadowEnable, _shadowEnabled);
            _receiver.BindBoolProperty(
                VmmCommands.FixedShadowAlwaysEnable,
                _fixedShadowEnabledAlways);
            _receiver.BindBoolProperty(
                VmmCommands.FixedShadowWhenLocomotionActiveEnable,
                _fixedShadowEnabledWhenLocomotionActive);

            _receiver.AssignCommandHandler(
                VmmCommands.ShadowIntensity,
                c => SetShadowIntensity(c.ParseAsPercentage())
                );
            
            InitializeBoardMaterialState();
         
            _shadowEnabled.CombineLatest(
                    _fixedShadowEnabledAlways,
                    _fixedShadowEnabledWhenLocomotionActive,
                    _bodyMotionModeController.MotionMode,
                    _vmcpReceiver.IsLocomotionReceiveSettingActive,
                    (shadowEnabled,
                        fixedShadowAlways,
                        fixedShadowWhenLocomotionActive,
                        motionMode,
                        isLocomotionReceiveSettingActive) =>
                    {
                        if (!shadowEnabled)
                        {
                            return false;
                        }
                        
                        if (fixedShadowAlways)
                        {
                            return true;
                        }

                        return fixedShadowWhenLocomotionActive &&
                            (motionMode is BodyMotionMode.GameInputLocomotion || isLocomotionReceiveSettingActive);
                    })
                .Subscribe(v => _fixedShadowEnabled.Value = v)
                .AddTo(this);

            _fixedShadowEnabled
                .Subscribe(enabled =>
                {
                    if (_fixedShadowBoardRenderer != null)
                    {
                        _fixedShadowBoardRenderer.gameObject.SetActive(enabled);
                    }
                })
                .AddTo(this);
        }

        private void InitializeBoardMaterialState()
        {
            if (_fixedShadowBoardRenderer?.sharedMaterial != null)
            {
                _shadowColor = _fixedShadowBoardRenderer.sharedMaterial.GetColor(ColorId);
            }

            ApplyBoardShadowColor();
        }

        private void SetShadowIntensity(float intensity)
        {
            _shadowIntensity = Mathf.Max(0f, intensity);
            ApplyBoardShadowColor();
        }

        private void ApplyBoardShadowColor()
        {
            if (_fixedShadowBoardRenderer == null)
            {
                return;
            }

            var color = _shadowColor;
            color.a *= _shadowIntensity;

            _fixedShadowBoardRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, color);
            _fixedShadowBoardRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
