using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Zenject;

namespace Baku.VMagicMirror
{
    public sealed class VmmAvatarDropShadowController : MonoBehaviour
    {
        private const string ShadowQuadShaderName = "Hidden/Vmm/AvatarDropShadowQuad";
        private static readonly int AvatarMaskTex = Shader.PropertyToID("_AvatarMaskTex");
        private static readonly int ShadowOffset = Shader.PropertyToID("_ShadowOffset");
        private static readonly int ShadowScale = Shader.PropertyToID("_ShadowScale");
        private static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
        private static readonly int AlphaThreshold = Shader.PropertyToID("_AlphaThreshold");

        public static VmmAvatarDropShadowController ActiveInstance { get; private set; }

        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private BackgroundImageBoard backgroundImageBoard = null;
        [SerializeField] private Renderer shadowQuadRenderer = null;
        [SerializeField] private float shadowDepthOffset = 0.4f;
        [SerializeField] private float minShadowDepth = 2.0f;
        [SerializeField] private float backgroundDepthMargin = 0.5f;

        public Renderer[] AvatarRenderers { get; private set; } = Array.Empty<Renderer>();
        public bool HasAvatar => AvatarRenderers.Length > 0;
        public RTHandle AvatarMaskHandle { get; private set; }

        public RTHandle AvatarMaskDepthHandle { get; private set; }

        public bool IsReady =>
            isActiveAndEnabled &&
            targetCamera != null &&
            AvatarMaskHandle != null &&
            AvatarMaskDepthHandle != null;

        private RenderTexture _avatarMaskTexture;
        private RenderTexture _avatarMaskDepthTexture;
        private Material _shadowQuadMaterial;

        private bool HasBackgroundImage => 
            backgroundImageBoard != null &&
            backgroundImageBoard.HasImage &&
            backgroundImageBoard.CachedRenderer.enabled;
        private float BackgroundEyeDepth => 
            HasBackgroundImage ? backgroundImageBoard.GetViewSpaceDepth(targetCamera) : 0f;

        [Inject]
        public void Initialize(IVRMLoadable vrmLoadable)
        {
            vrmLoadable.VrmLoaded += info => AvatarRenderers = info.renderers ?? Array.Empty<Renderer>();
            vrmLoadable.VrmDisposing += () => AvatarRenderers = Array.Empty<Renderer>();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            EnsureMaskTextures();
            EnsureShadowQuadMaterial();
            UpdateShadowQuad();
        }

        private void LateUpdate()
        {
            if (ActiveInstance != this)
            {
                ActiveInstance = this;
            }

            EnsureMaskTextures();
            EnsureShadowQuadMaterial();
            UpdateShadowQuad();
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (shadowQuadRenderer != null)
            {
                shadowQuadRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            ReleaseMaskTextures();
            if (_shadowQuadMaterial != null)
            {
                UnityEngine.Object.Destroy(_shadowQuadMaterial);
                _shadowQuadMaterial = null;
            }
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
            AvatarMaskHandle = RTHandles.Alloc(_avatarMaskTexture);

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
            AvatarMaskDepthHandle = RTHandles.Alloc(_avatarMaskDepthTexture);
        }

        private void ReleaseMaskTextures()
        {
            AvatarMaskHandle?.Release();
            AvatarMaskDepthHandle?.Release();
            AvatarMaskHandle = null;
            AvatarMaskDepthHandle = null;

            if (_avatarMaskTexture != null)
            {
                if (_avatarMaskTexture.IsCreated())
                {
                    _avatarMaskTexture.Release();
                }

                UnityEngine.Object.Destroy(_avatarMaskTexture);
                _avatarMaskTexture = null;
            }

            if (_avatarMaskDepthTexture != null)
            {
                if (_avatarMaskDepthTexture.IsCreated())
                {
                    _avatarMaskDepthTexture.Release();
                }

                UnityEngine.Object.Destroy(_avatarMaskDepthTexture);
                _avatarMaskDepthTexture = null;
            }
        }

        private void EnsureShadowQuadMaterial()
        {
            if (shadowQuadRenderer == null || _shadowQuadMaterial != null)
            {
                return;
            }

            var shader = Shader.Find(ShadowQuadShaderName);
            if (shader == null)
            {
                return;
            }

            _shadowQuadMaterial = new Material(shader)
            {
                name = "VmmAvatarDropShadowQuad (Runtime)"
            };
            shadowQuadRenderer.material = _shadowQuadMaterial;
        }

        private void UpdateShadowQuad()
        {
            if (shadowQuadRenderer == null)
            {
                return;
            }

            var volume = VmmAvatarDropShadowVolume.GetActiveComponent();
            var active =　HasAvatar;

            shadowQuadRenderer.enabled = active;
            if (!active)
            {
                return;
            }

            var depth = ComputeShadowDepth();
            UpdateShadowQuadTransform(depth);
            UpdateShadowQuadMaterial(volume);
        }

        private float ComputeShadowDepth()
        {
            var farLimit = Mathf.Max(minShadowDepth, targetCamera.farClipPlane - backgroundDepthMargin);
            var avatarDepth = GetAvatarBackDepth() + shadowDepthOffset;
            var depth = Mathf.Max(minShadowDepth, avatarDepth);

            if (HasBackgroundImage)
            {
                depth = Mathf.Min(depth, BackgroundEyeDepth - backgroundDepthMargin);
            }

            return Mathf.Clamp(depth, minShadowDepth, farLimit);
        }

        private float GetAvatarBackDepth()
        {
            var hasBounds = false;
            var maxDepth = minShadowDepth;
            foreach (var r in AvatarRenderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = r.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var depth = targetCamera.transform.InverseTransformPoint(GetBoundsCorner(bounds, i)).z;
                    maxDepth = Mathf.Max(maxDepth, depth);
                    hasBounds = true;
                }
            }

            return hasBounds ? maxDepth : minShadowDepth;
        }

        private void UpdateShadowQuadTransform(float depth)
        {
            var transformRef = shadowQuadRenderer.transform;
            transformRef.localPosition = new Vector3(0f, 0f, depth);

            var yScale = Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * depth * 2f;
            var xScale = targetCamera.aspect * yScale;
            transformRef.localScale = new Vector3(xScale, yScale, 1f);
        }

        private void UpdateShadowQuadMaterial(VmmAvatarDropShadowVolume volume)
        {
            _shadowQuadMaterial.SetTexture(AvatarMaskTex, AvatarMaskHandle.rt);
            _shadowQuadMaterial.SetVector(ShadowOffset, volume.offset.value);
            _shadowQuadMaterial.SetVector(ShadowScale, volume.scale.value);
            _shadowQuadMaterial.SetColor(ShadowColor, volume.color.value);
            _shadowQuadMaterial.SetFloat(AlphaThreshold, volume.alphaThreshold.value);
        }

        private static Vector3 GetBoundsCorner(Bounds bounds, int index)
        {
            var extents = bounds.extents;
            var center = bounds.center;
            return center + new Vector3(
                (index & 1) == 0 ? -extents.x : extents.x,
                (index & 2) == 0 ? -extents.y : extents.y,
                (index & 4) == 0 ? -extents.z : extents.z);
        }
    }
}
