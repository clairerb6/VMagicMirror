using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace Baku.VMagicMirror
{
    public class AntiAliasSettingSetter : PresenterBase
    {
        private readonly Camera _mainCamera;
        private readonly IMessageReceiver _receiver;
        private UniversalAdditionalCameraData _additionalCameraData;
        
        [Inject]
        public AntiAliasSettingSetter(Camera mainCamera, IMessageReceiver receiver)
        {
            _mainCamera = mainCamera;
            _receiver = receiver;
        }

        public override void Initialize()
        {
            _additionalCameraData = _mainCamera.GetUniversalAdditionalCameraData();
            _receiver.AssignCommandHandler(
                VmmCommands.SetAntiAliasStyle, 
                command => SetAntiAliasStyle(command.ToInt())
                );
        }
        
        private void SetAntiAliasStyle(int value)
        {
            if (value < 0 || value > (int)AntiAliasStyles.High)
            {
                return;
            }

            var style = (AntiAliasStyles)value;

            _additionalCameraData.antialiasing = style == AntiAliasStyles.None
                ? AntialiasingMode.None
                : AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            _additionalCameraData.antialiasingQuality = style switch
            {
                AntiAliasStyles.High => AntialiasingQuality.High,
                AntiAliasStyles.Mid => AntialiasingQuality.Medium,
                _ => AntialiasingQuality.Low
            };
        }
    }

    public enum AntiAliasStyles
    {
        None = 0,
        Low = 1,
        Mid = 2,
        High = 3,
    }
}
