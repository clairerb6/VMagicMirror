using Zenject;

namespace Baku.VMagicMirror
{
    public sealed class AvatarOffsetRimController : PresenterBase
    {
        private readonly IMessageReceiver _receiver;
        private readonly AvatarMaskTextureController _avatarMaskTextureController;
        
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
            // TODO:
            // - 実際にはmessage receiver経由でオンオフする (が、それは後でいい)
            // - VolumeComponentのenableもここで設定したいが、
            _avatarMaskTextureController.SetAvatarOffsetRimEnabled(true);
        }
    }
}
