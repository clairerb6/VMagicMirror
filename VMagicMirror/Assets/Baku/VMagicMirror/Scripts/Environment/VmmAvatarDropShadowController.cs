using UnityEngine;
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
        private static readonly int ShadowBlurStep = Shader.PropertyToID("_ShadowBlurStep");
        private static readonly int AlphaThreshold = Shader.PropertyToID("_AlphaThreshold");
        private static readonly int MaskOverscanInv = Shader.PropertyToID("_MaskOverscanInv");
        private const string ShadowBlurKeyword = "_VMM_SHADOW_BLUR";
        private const float AlphaThresholdValue = 0.001f;
        private const float ShadowBlurWorldUnit = 0.001f;
        private const float MinShadowDepth = 0.01f;

        [SerializeField] private Camera targetCamera = null;
        [SerializeField] private BackgroundImageBoard backgroundImageBoard = null;
        [SerializeField] private Renderer shadowQuadRenderer = null;
        [SerializeField] private float shadowDepthOffset = 0.4f;
        [SerializeField] private float backgroundDepthMargin = 0.5f;
        [SerializeField] private Color shadowColor = Color.black;
        [SerializeField] private float shadowIntensity = 0.65f;
        [SerializeField] private int shadowBlur = 10;
        [SerializeField] private float shadowYawDeg = -20f;
        [SerializeField] private float shadowPitchDeg = 8f;

        private Material _shadowQuadMaterial;
        // NOTE: componentのenabledではない形でon/offを制御しとく
        private bool _enabled = true;

        private bool HasBackgroundImage => 
            backgroundImageBoard != null &&
            backgroundImageBoard.HasImage &&
            backgroundImageBoard.CachedRenderer.enabled;
        private float BackgroundEyeDepth => 
            HasBackgroundImage ? backgroundImageBoard.GetViewSpaceDepth(targetCamera) : 0f;

        private AvatarMaskTextureController _avatarMaskTextureController;
        
        [Inject]
        public void Initialize(AvatarMaskTextureController avatarMaskTextureController)
        {
            _avatarMaskTextureController = avatarMaskTextureController;
            EnsureShadowQuadMaterial();
        }

        public void SetEnabled(bool enable)
        {
            if (_enabled == enable)
            {
                return;
            }

            _enabled = enable;
            _avatarMaskTextureController.SetAvatarDropShadowEnabled(enable);
            if (enable)
            {
                UpdateShadowQuad();
            }
        }

        public void SetDepthOffset(float offset) => shadowDepthOffset = offset;
        public void SetShadowColor(float r, float g, float b) => shadowColor = new Color(r, g, b);
        public void SetShadowBlur(int blur)
        {
            shadowBlur = Mathf.Clamp(blur, 0, 100);
            ApplyShadowBlurKeyword();
        }
        public void SetShadowIntensity(float intensity) => shadowIntensity = intensity;
        public void SetShadowYaw(int yawDeg) => shadowYawDeg = yawDeg;
        public void SetShadowPitch(int pitchDeg) => shadowPitchDeg = pitchDeg;

        private void LateUpdate()
        {
            if (_enabled)
            {
                UpdateShadowQuad();
            }
            else
            {
                shadowQuadRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_shadowQuadMaterial != null)
            {
                Destroy(_shadowQuadMaterial);
                _shadowQuadMaterial = null;
            }
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
            ApplyShadowBlurKeyword();
        }

        private void UpdateShadowQuad()
        {
            var active = _avatarMaskTextureController.HasAvatar && _enabled;

            shadowQuadRenderer.enabled = active;
            if (!active)
            {
                return;
            }

            var avatarBackDepth = GetAvatarBackDepth();
            var depth = ComputeShadowDepth(avatarBackDepth);
            UpdateShadowQuadTransform(depth);
            UpdateShadowQuadMaterial(depth, avatarBackDepth);
        }

        private float ComputeShadowDepth(float avatarBackDepth)
        {
            var farLimit = Mathf.Max(MinShadowDepth, targetCamera.farClipPlane - backgroundDepthMargin);
            var avatarDepth = avatarBackDepth + shadowDepthOffset;
            var depth = Mathf.Max(MinShadowDepth, avatarDepth);

            if (HasBackgroundImage)
            {
                depth = Mathf.Min(depth, BackgroundEyeDepth - backgroundDepthMargin);
            }

            return Mathf.Clamp(depth, MinShadowDepth, farLimit);
        }

        private float GetAvatarBackDepth()
        {
            var hasBounds = false;
            var maxDepth = float.NegativeInfinity;
            foreach (var r in _avatarMaskTextureController.AvatarRenderers)
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

            return hasBounds ? maxDepth : MinShadowDepth;
        }

        private void UpdateShadowQuadTransform(float depth)
        {
            var transformRef = shadowQuadRenderer.transform;
            transformRef.localPosition = new Vector3(0f, 0f, depth);

            var yScale = Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * depth * 2f;
            var xScale = targetCamera.aspect * yScale;
            transformRef.localScale = new Vector3(xScale, yScale, 1f);
        }

        private void UpdateShadowQuadMaterial(float depth, float avatarBackDepth)
        {
            _shadowQuadMaterial.SetTexture(AvatarMaskTex, _avatarMaskTextureController.AvatarMaskHandle.rt);
            _shadowQuadMaterial.SetFloat(MaskOverscanInv, 1.0f / _avatarMaskTextureController.AvatarMaskOverscanFactor);

            var offset = CalculateShadowOffset(depth, avatarBackDepth);
            var scale = CalculateShadowScale(depth, avatarBackDepth);
            var color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowIntensity);

            _shadowQuadMaterial.SetVector(ShadowOffset, offset);
            _shadowQuadMaterial.SetVector(ShadowScale, Vector2.one * scale);
            _shadowQuadMaterial.SetColor(ShadowColor, color);
            _shadowQuadMaterial.SetVector(ShadowBlurStep, CalculateShadowBlurStep(depth));
            _shadowQuadMaterial.SetFloat(AlphaThreshold, AlphaThresholdValue);
        }

        private void ApplyShadowBlurKeyword()
        {
            if (_shadowQuadMaterial == null)
            {
                return;
            }

            if (shadowBlur > 0)
            {
                _shadowQuadMaterial.EnableKeyword(ShadowBlurKeyword);
            }
            else
            {
                _shadowQuadMaterial.DisableKeyword(ShadowBlurKeyword);
            }
        }

        private Vector2 CalculateShadowBlurStep(float shadowDepth)
        {
            if (shadowBlur <= 0)
            {
                return Vector2.zero;
            }

            var safeDepth = Mathf.Max(0.0001f, shadowDepth);
            var tanHalfVerticalFov = Mathf.Max(
                0.0001f,
                Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f));
            var tanHalfHorizontalFov = Mathf.Max(0.0001f, tanHalfVerticalFov * targetCamera.aspect);
            var worldRadius = shadowBlur * ShadowBlurWorldUnit;

            return new Vector2(
                worldRadius / (2f * safeDepth * tanHalfHorizontalFov),
                worldRadius / (2f * safeDepth * tanHalfVerticalFov)
            );
        }

        private Vector2 CalculateShadowOffset(float shadowDepth, float avatarBackDepth)
        {
            var safeShadowDepth = Mathf.Max(0.0001f, shadowDepth);
            var depthRate = Mathf.Max(0f, shadowDepth - avatarBackDepth) / safeShadowDepth;
            var tanHalfVerticalFov = Mathf.Max(
                0.0001f,
                Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f));
            var tanHalfHorizontalFov = Mathf.Max(0.0001f, tanHalfVerticalFov * targetCamera.aspect);

            return new Vector2(
                0.5f * depthRate * Mathf.Tan(shadowYawDeg * Mathf.Deg2Rad) / tanHalfHorizontalFov,
                -0.5f * depthRate * Mathf.Tan(shadowPitchDeg * Mathf.Deg2Rad) / tanHalfVerticalFov
            );
        }

        private static float CalculateShadowScale(float shadowDepth, float avatarBackDepth)
        {
            var safeShadowDepth = Mathf.Max(0.0001f, shadowDepth);
            var casterDepth = Mathf.Clamp(avatarBackDepth, 0.0001f, safeShadowDepth);
            return casterDepth / safeShadowDepth;
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
