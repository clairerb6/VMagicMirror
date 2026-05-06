using R3;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror
{
    public sealed class AvatarOffsetRimController : PresenterBase
    {
        private const float ThicknessScale = 0.0005f;
        
        private readonly IMessageReceiver _receiver;
        private readonly AvatarMaskTextureController _avatarMaskTextureController;

        private readonly ReactiveProperty<float> _rimIntensity = new(0f);
        // NOTE: 無次元量です
        private readonly ReactiveProperty<int> _rimThickness = new(5);
        // NOTE: この値に90degオフセットしたものをshaderに渡す、「0 = 真上」という建付けにしたいので
        private readonly ReactiveProperty<int> _rimAngle = new(0);
        private readonly ReactiveProperty<Color> _rimColor = new(Color.white);
        
        [Inject]
        public AvatarOffsetRimController(
            IMessageReceiver receiver,
            AvatarMaskTextureController avatarMaskTextureController)
        {
            _receiver = receiver;
            _avatarMaskTextureController = avatarMaskTextureController;
        }
        
        public override void Initialize()
        {
            _receiver.BindPercentageProperty(VmmCommands.SetRimIntensity, _rimIntensity);
            _receiver.BindIntProperty(VmmCommands.SetRimThickness, _rimThickness);
            _receiver.BindIntProperty(VmmCommands.SetRimAngle, _rimAngle);
            _receiver.BindColorProperty(VmmCommands.SetRimColor, _rimColor);

            _rimIntensity.CombineLatest(_rimThickness, (intensity, thickness) => intensity > 0f && thickness > 0)
                .DistinctUntilChanged()
                .Subscribe(SetRimEnabled)
                .AddTo(this);
         
            _rimIntensity.Subscribe(value => 
                    VmmVolumeComponentAccessor.UpdateAvatarOffsetRim(volume => volume.applyRate.value = value))
                .AddTo(this);

            _rimColor.Subscribe(color => 
                    VmmVolumeComponentAccessor.UpdateAvatarOffsetRim(volume => volume.rimColor.value = color))
                .AddTo(this);

            _rimAngle.CombineLatest(_rimThickness, (angle, thickness) => (angle, thickness))
                .DistinctUntilChanged()
                .Subscribe(value =>
                {
                    // 極座標から普通の座標に修正し、かつthicknessのスケールをよしなにする
                    var length = value.thickness * ThicknessScale;
                    var angleRad = Mathf.Deg2Rad * (value.angle + 90);
                    var offset = new Vector2(
                        math.cos(angleRad) * length,
                        math.sin(angleRad) * length
                    );
                    VmmVolumeComponentAccessor.UpdateAvatarOffsetRim(volume => volume.offset.value = offset);
                })
                .AddTo(this);
        }

        private void SetRimEnabled(bool enabled)
        {
            _avatarMaskTextureController.SetAvatarOffsetRimEnabled(enabled);
            VmmVolumeComponentAccessor.SetVmmAvatarOffsetRimActive(enabled);
        }
    }
}
