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
        private static readonly int MaskOverscanInv = Shader.PropertyToID("_MaskOverscanInv");
        private const float AlphaThresholdValue = 0.001f;

        public static VmmAvatarDropShadowController ActiveInstance { get; private set; }

        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private BackgroundImageBoard backgroundImageBoard = null;
        [SerializeField] private Renderer shadowQuadRenderer = null;
        [SerializeField] private float shadowDepthOffset = 0.4f;
        [SerializeField] private float minShadowDepth = 2.0f;
        [SerializeField] private float backgroundDepthMargin = 0.5f;
        [SerializeField] private float shadowIntensity = 0.65f;
        [SerializeField] private float shadowYawDeg = -20f;
        [SerializeField] private float shadowPitchDeg = 8f;

        [SerializeField, Range(-0.01f, 0.01f)] private float yawFactor = 0.003f;
        [SerializeField, Range(-0.01f, 0.01f)] private float pitchFactor = -0.003f;
        [SerializeField] private float shadowScale = 0.85f;
        [SerializeField, Range(1.0f, 2.0f)] private float maskOverscanFactor = 1.5f;

        public bool IsReady => _enabled && _hasRenderTexture;
        
        public Renderer[] AvatarRenderers { get; private set; } = Array.Empty<Renderer>();
        public bool HasAvatar => AvatarRenderers.Length > 0;
        public RTHandle AvatarMaskHandle { get; private set; }
        public RTHandle AvatarMaskDepthHandle { get; private set; }
        public float AvatarMaskOverscanFactor => maskOverscanFactor;

        // NOTE: Unityのnull checkをしたくないので明示的にフラグを持つ
        private bool _hasRenderTexture;
        private RenderTexture _avatarMaskTexture;
        private RenderTexture _avatarMaskDepthTexture;
        private Material _shadowQuadMaterial;
        // NOTE: componentのenabledではない形でon/offを制御しとく
        private bool _enabled = true;

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
            ActiveInstance = this;
            EnsureShadowQuadMaterial();
        }

        public void SetEnabled(bool enable)
        {
            if (_enabled == enable)
            {
                return;
            }

            _enabled = enable;
            if (enable)
            {
                EnsureMaskTextures();
                UpdateShadowQuad();
            }
            else
            {
                ReleaseMaskTextures();
            }
        }

        public void SetDepthOffset(float offset) => shadowDepthOffset = offset;
        public void SetShadowIntensity(float intensity) => shadowIntensity = intensity;
        public void SetShadowYaw(int yawDeg) => shadowYawDeg = yawDeg;
        public void SetShadowPitch(int pitchDeg) => shadowPitchDeg = pitchDeg;

        private void LateUpdate()
        {
            if (_enabled)
            {
                EnsureMaskTextures();
                UpdateShadowQuad();
            }
            else
            {
                shadowQuadRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
            ReleaseMaskTextures();
            if (_shadowQuadMaterial != null)
            {
                Destroy(_shadowQuadMaterial);
                _shadowQuadMaterial = null;
            }
        }

        private void EnsureMaskTextures()
        {
            var width = Mathf.Max(1, targetCamera.scaledPixelWidth);
            var height = Mathf.Max(1, targetCamera.scaledPixelHeight);
            if (_hasRenderTexture && 
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
            _hasRenderTexture = true;
        }

        private void ReleaseMaskTextures()
        {
            if (!_hasRenderTexture)
            {
                return;
            }

            _hasRenderTexture = false;
            AvatarMaskHandle?.Release();
            AvatarMaskDepthHandle?.Release();
            AvatarMaskHandle = null;
            AvatarMaskDepthHandle = null;

            if (_avatarMaskTexture.IsCreated())
            {
                _avatarMaskTexture.Release();
            }
            Destroy(_avatarMaskTexture);
            _avatarMaskTexture = null;

            if (_avatarMaskDepthTexture.IsCreated())
            {
                _avatarMaskDepthTexture.Release();
            }
            Destroy(_avatarMaskDepthTexture);
            _avatarMaskDepthTexture = null;
        }

        private void EnsureShadowQuadMaterial()
        {
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
            var active = HasAvatar && _enabled;

            shadowQuadRenderer.enabled = active;
            if (!active)
            {
                return;
            }

            var depth = ComputeShadowDepth();
            UpdateShadowQuadTransform(depth);
            UpdateShadowQuadMaterial();
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

        private void UpdateShadowQuadMaterial()
        {
            _shadowQuadMaterial.SetTexture(AvatarMaskTex, AvatarMaskHandle.rt);
            _shadowQuadMaterial.SetFloat(MaskOverscanInv, 1.0f / AvatarMaskOverscanFactor);

            // depthが伸びると影が見かけ奥まったように見せるためにoffsetもデカくなる
            var offsetScaleByDepth = shadowDepthOffset / 0.4f; 
            var offset = new Vector2(
                shadowYawDeg * yawFactor * offsetScaleByDepth,
                shadowPitchDeg * pitchFactor * offsetScaleByDepth
            );

            // NOTE: scaleは実は固定で良くて、depthのコントロールだけで良い、というのはある？あるかも。
            //var scale = Vector2.one;
            var color = new Color(0f, 0f, 0f, shadowIntensity);
            
            _shadowQuadMaterial.SetVector(ShadowOffset, offset);
            _shadowQuadMaterial.SetVector(ShadowScale, 
                Vector2.one * (shadowScale * CalculateShadowScale(shadowDepthOffset)));
            _shadowQuadMaterial.SetColor(ShadowColor, color);
            _shadowQuadMaterial.SetFloat(AlphaThreshold, AlphaThresholdValue);
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

        private static float CalculateShadowScale(float depth)
        {
            // depth = 0m のとき scale = 1.0
            // depth = 2.5m = 250cm (GUI側の最大値) のとき scale = 0.6
            // くらいになるように線形にやってる
            return Mathf.Lerp(1.0f, 0.6f, depth / 2.5f);
        }
    }
}
