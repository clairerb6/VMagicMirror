using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Zenject;

namespace Baku.VMagicMirror
{
    public sealed class VmmAvatarDropShadowController : MonoBehaviour
    {
        public static VmmAvatarDropShadowController ActiveInstance { get; private set; }

        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private BackgroundImageBoard backgroundImageBoard = null;

        public Renderer[] AvatarRenderers { get; private set; } = System.Array.Empty<Renderer>();
        public bool HasAvatar => AvatarRenderers.Length > 0;
        public bool HasBackgroundImage => backgroundImageBoard != null && backgroundImageBoard.HasImage;
        public bool HasOpaqueCameraBackground =>
            !HasBackgroundImage &&
            targetCamera != null &&
            targetCamera.backgroundColor.a > 0.001f;
        public float BackgroundEyeDepth => HasBackgroundImage ? backgroundImageBoard.GetViewSpaceDepth(targetCamera) : 0f;
        public RTHandle AvatarMaskHandle => _avatarMaskHandle;
        public RTHandle AvatarMaskDepthHandle => _avatarMaskDepthHandle;
        public bool IsReady =>
            isActiveAndEnabled &&
            targetCamera != null &&
            _avatarMaskHandle != null &&
            _avatarMaskDepthHandle != null &&
            HasAvatar;

        private RenderTexture _avatarMaskTexture;
        private RenderTexture _avatarMaskDepthTexture;
        private RTHandle _avatarMaskHandle;
        private RTHandle _avatarMaskDepthHandle;

        [Inject]
        public void Initialize(IVRMLoadable vrmLoadable)
        {
            vrmLoadable.VrmLoaded += info => AvatarRenderers = info.renderers ?? System.Array.Empty<Renderer>();
            vrmLoadable.VrmDisposing += () => AvatarRenderers = System.Array.Empty<Renderer>();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            EnsureMaskTextures();
        }

        private void LateUpdate()
        {
            if (ActiveInstance != this)
            {
                ActiveInstance = this;
            }

            EnsureMaskTextures();
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseMaskTextures();
        }

        private void EnsureMaskTextures()
        {
            if (targetCamera == null)
            {
                return;
            }

            var width = Mathf.Max(1, targetCamera.scaledPixelWidth);
            var height = Mathf.Max(1, targetCamera.scaledPixelHeight);
            if (_avatarMaskTexture != null &&
                _avatarMaskDepthTexture != null &&
                _avatarMaskTexture.width == width &&
                _avatarMaskTexture.height == height &&
                _avatarMaskDepthTexture.width == width &&
                _avatarMaskDepthTexture.height == height)
            {
                return;
            }

            ReleaseMaskTextures();

            var colorDescriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.R8_UNorm,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                mipCount = 1,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };
            _avatarMaskTexture = new RenderTexture(colorDescriptor)
            {
                name = "VmmAvatarMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _avatarMaskTexture.Create();
            _avatarMaskHandle = RTHandles.Alloc(_avatarMaskTexture);

            var depthDescriptor = new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = GraphicsFormat.None,
                depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt,
                msaaSamples = 1,
                mipCount = 1,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };
            _avatarMaskDepthTexture = new RenderTexture(depthDescriptor)
            {
                name = "VmmAvatarMaskDepth",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _avatarMaskDepthTexture.Create();
            _avatarMaskDepthHandle = RTHandles.Alloc(_avatarMaskDepthTexture);
        }

        private void ReleaseMaskTextures()
        {
            _avatarMaskHandle?.Release();
            _avatarMaskDepthHandle?.Release();
            _avatarMaskHandle = null;
            _avatarMaskDepthHandle = null;

            if (_avatarMaskTexture != null)
            {
                if (_avatarMaskTexture.IsCreated())
                {
                    _avatarMaskTexture.Release();
                }

                Destroy(_avatarMaskTexture);
                _avatarMaskTexture = null;
            }

            if (_avatarMaskDepthTexture != null)
            {
                if (_avatarMaskDepthTexture.IsCreated())
                {
                    _avatarMaskDepthTexture.Release();
                }

                Destroy(_avatarMaskDepthTexture);
                _avatarMaskDepthTexture = null;
            }
        }
    }
}
