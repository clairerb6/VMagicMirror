using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    public sealed class ShadowMapBoardController : MonoBehaviour
    {
        private const string ShadowBoardShaderName = "Custom/ShadowMapBoardDrawer";

        private static readonly int ShadowMapId = Shader.PropertyToID("_ShadowMap");
        private static readonly int ShadowMapViewProjId = Shader.PropertyToID("_ShadowMapViewProj");
        private static readonly int ShadowMapDepthBiasId = Shader.PropertyToID("_ShadowMapDepthBias");

        public static ShadowMapBoardController ActiveInstance { get; private set; }

        [SerializeField] private Light shadowLight = null;
        [SerializeField] private Renderer shadowBoardRenderer = null;
        [SerializeField] private int shadowMapSize = 1024;
        [SerializeField] private LayerMask shadowCasterLayerMask = ~0;
        [SerializeField] private Transform[] exclusionRoots = null;
        [SerializeField] private float maxCasterDistance = 8.0f;
        [SerializeField] private float projectionPadding = 0.15f;
        [SerializeField] private float nearClipPlane = 0.01f;
        [SerializeField] private float minFarClipPlane = 6.0f;
        [SerializeField] private float depthBias = 0.0025f;

        public Matrix4x4 ShadowViewMatrix { get; private set; }
        public Matrix4x4 ShadowProjectionMatrix { get; private set; }
        public float DepthBias => depthBias;
        public RTHandle ShadowColorHandle => _shadowColorHandle;
        public RTHandle ShadowDepthHandle => _shadowDepthHandle;
        public bool IsReady =>
            isActiveAndEnabled &&
            shadowLight != null &&
            shadowBoardRenderer != null &&
            shadowBoardRenderer.enabled &&
            shadowBoardRenderer.gameObject.activeInHierarchy &&
            _shadowColorHandle != null &&
            _shadowDepthHandle != null;

        private RenderTexture _shadowColorTexture;
        private RenderTexture _shadowDepthTexture;
        private RTHandle _shadowColorHandle;
        private RTHandle _shadowDepthHandle;
        private MaterialPropertyBlock _propertyBlock;
        private Material _runtimeMaterial;
        private Renderer[] _shadowCasters = System.Array.Empty<Renderer>();
        private int _shadowCastersFrame = -1;

        private void OnEnable()
        {
            ActiveInstance = this;
            _propertyBlock ??= new MaterialPropertyBlock();
            EnsureRenderTextures();
            EnsureBoardMaterial();
            UpdateShadowMatrices();
            ApplyMaterialProperties();
        }

        private void LateUpdate()
        {
            if (ActiveInstance != this)
            {
                ActiveInstance = this;
            }

            EnsureRenderTextures();
            EnsureBoardMaterial();
            UpdateShadowMatrices();
            ApplyMaterialProperties();
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (shadowBoardRenderer != null)
            {
                shadowBoardRenderer.SetPropertyBlock(null);
            }
        }

        private void OnDestroy()
        {
            ReleaseRenderTextures();
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public Renderer[] GetShadowCasters()
        {
            if (_shadowCastersFrame == Time.frameCount)
            {
                return _shadowCasters;
            }

            _shadowCastersFrame = Time.frameCount;
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var casters = new List<Renderer>(allRenderers.Length);
            foreach (var renderer in allRenderers)
            {
                if (IsValidShadowCaster(renderer))
                {
                    casters.Add(renderer);
                }
            }

            _shadowCasters = casters.ToArray();
            return _shadowCasters;
        }

        public void EnsureRenderTextures()
        {
            var size = Mathf.Max(64, shadowMapSize);
            if (_shadowColorTexture != null &&
                _shadowDepthTexture != null &&
                _shadowColorTexture.width == size &&
                _shadowColorTexture.height == size &&
                _shadowDepthTexture.width == size &&
                _shadowDepthTexture.height == size)
            {
                return;
            }

            ReleaseRenderTextures();

            var colorDescriptor = new RenderTextureDescriptor(size, size)
            {
                graphicsFormat = GraphicsFormat.R32_SFloat,
                depthStencilFormat = GraphicsFormat.None,
                msaaSamples = 1,
                mipCount = 1,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };
            _shadowColorTexture = new RenderTexture(colorDescriptor)
            {
                name = "ShadowBoardShadowMap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _shadowColorTexture.Create();
            _shadowColorHandle = RTHandles.Alloc(_shadowColorTexture);

            var depthDescriptor = new RenderTextureDescriptor(size, size)
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
            _shadowDepthTexture = new RenderTexture(depthDescriptor)
            {
                name = "ShadowBoardShadowDepth",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _shadowDepthTexture.Create();
            _shadowDepthHandle = RTHandles.Alloc(_shadowDepthTexture);
        }

        private void ReleaseRenderTextures()
        {
            _shadowColorHandle?.Release();
            _shadowDepthHandle?.Release();
            _shadowColorHandle = null;
            _shadowDepthHandle = null;

            if (_shadowColorTexture != null)
            {
                if (_shadowColorTexture.IsCreated())
                {
                    _shadowColorTexture.Release();
                }
                Destroy(_shadowColorTexture);
                _shadowColorTexture = null;
            }

            if (_shadowDepthTexture != null)
            {
                if (_shadowDepthTexture.IsCreated())
                {
                    _shadowDepthTexture.Release();
                }
                Destroy(_shadowDepthTexture);
                _shadowDepthTexture = null;
            }
        }

        private void EnsureBoardMaterial()
        {
            if (shadowBoardRenderer == null || _runtimeMaterial != null)
            {
                return;
            }

            var shader = Shader.Find(ShadowBoardShaderName);
            if (shader == null)
            {
                return;
            }

            var sourceMaterial = shadowBoardRenderer.sharedMaterial;
            _runtimeMaterial = sourceMaterial != null
                ? new Material(sourceMaterial)
                : new Material(shader);
            _runtimeMaterial.name = (sourceMaterial != null ? sourceMaterial.name : "ShadowBoard") + " (ShadowMap)";
            _runtimeMaterial.shader = shader;
            shadowBoardRenderer.material = _runtimeMaterial;
        }

        private bool IsValidShadowCaster(Renderer renderer)
        {
            if (renderer == null ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (renderer == shadowBoardRenderer)
            {
                return false;
            }

            if (renderer.transform.IsChildOf(transform))
            {
                return false;
            }

            if (((1 << renderer.gameObject.layer) & shadowCasterLayerMask.value) == 0)
            {
                return false;
            }

            if (exclusionRoots != null)
            {
                foreach (var exclusionRoot in exclusionRoots)
                {
                    if (exclusionRoot != null && renderer.transform.IsChildOf(exclusionRoot))
                    {
                        return false;
                    }
                }
            }

            if (shadowBoardRenderer != null)
            {
                var referencePoint = shadowBoardRenderer.bounds.center;
                var nearestPoint = renderer.bounds.ClosestPoint(referencePoint);
                if (Vector3.Distance(referencePoint, nearestPoint) > maxCasterDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateShadowMatrices()
        {
            if (shadowLight == null || shadowBoardRenderer == null)
            {
                return;
            }

            var casters = GetShadowCasters();
            if (casters.Length == 0)
            {
                var boardBounds = shadowBoardRenderer.bounds;
                var focus = boardBounds.center;
                var position = focus - shadowLight.transform.forward * Mathf.Max(2.0f, maxCasterDistance);
                ShadowViewMatrix = Matrix4x4.LookAt(position, focus, shadowLight.transform.up);
                ShadowProjectionMatrix = Matrix4x4.Ortho(
                    -boardBounds.extents.x - projectionPadding,
                    boardBounds.extents.x + projectionPadding,
                    -boardBounds.extents.z - projectionPadding,
                    boardBounds.extents.z + projectionPadding,
                    nearClipPlane,
                    minFarClipPlane);
                return;
            }

            BuildShadowMatrices(casters, shadowLight.transform.forward, out var forwardView, out var forwardProj);
            BuildShadowMatrices(casters, -shadowLight.transform.forward, out var reverseView, out var reverseProj);

            var forwardScore = ScoreShadowMatrices(casters, shadowBoardRenderer.bounds.center, forwardView, forwardProj);
            var reverseScore = ScoreShadowMatrices(casters, shadowBoardRenderer.bounds.center, reverseView, reverseProj);

            if (reverseScore > forwardScore)
            {
                ShadowViewMatrix = reverseView;
                ShadowProjectionMatrix = reverseProj;
            }
            else
            {
                ShadowViewMatrix = forwardView;
                ShadowProjectionMatrix = forwardProj;
            }
        }

        private void ApplyMaterialProperties()
        {
            if (shadowBoardRenderer == null || _shadowColorTexture == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            shadowBoardRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(ShadowMapId, _shadowColorTexture);
            _propertyBlock.SetMatrix(
                ShadowMapViewProjId,
                GL.GetGPUProjectionMatrix(ShadowProjectionMatrix, true) * ShadowViewMatrix);
            _propertyBlock.SetFloat(ShadowMapDepthBiasId, depthBias);
            shadowBoardRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void BuildShadowMatrices(
            Renderer[] casters,
            Vector3 lookDirection,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix)
        {
            var forward = lookDirection.sqrMagnitude > 0.0f ? lookDirection.normalized : Vector3.forward;
            var up = shadowLight.transform.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f)
            {
                up = Vector3.up;
            }

            var combinedBounds = casters[0].bounds;
            for (var i = 1; i < casters.Length; i++)
            {
                combinedBounds.Encapsulate(casters[i].bounds);
            }
            combinedBounds.Encapsulate(shadowBoardRenderer.bounds);

            var focus = combinedBounds.center;
            var position = focus - forward * Mathf.Max(maxCasterDistance, combinedBounds.extents.magnitude + projectionPadding);
            viewMatrix = Matrix4x4.LookAt(position, focus, up);

            GetViewSpaceBounds(casters, shadowBoardRenderer.bounds, viewMatrix, out var min, out var max);

            var pullBack = Mathf.Max(0.0f, max.z + nearClipPlane + projectionPadding);
            if (pullBack > 0.0f)
            {
                position -= forward * pullBack;
                viewMatrix = Matrix4x4.LookAt(position, focus, up);
                GetViewSpaceBounds(casters, shadowBoardRenderer.bounds, viewMatrix, out min, out max);
            }

            min -= Vector3.one * projectionPadding;
            max += Vector3.one * projectionPadding;

            projectionMatrix = Matrix4x4.Ortho(
                min.x,
                max.x,
                min.y,
                max.y,
                nearClipPlane,
                Mathf.Max(minFarClipPlane, -min.z));
        }

        private static int ScoreShadowMatrices(
            Renderer[] casters,
            Vector3 boardCenter,
            Matrix4x4 viewMatrix,
            Matrix4x4 projectionMatrix)
        {
            var viewProj = GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            var score = ScorePoint(viewProj, boardCenter) * 2;
            foreach (var renderer in casters)
            {
                score += ScorePoint(viewProj, renderer.bounds.center);
            }
            return score;
        }

        private static int ScorePoint(Matrix4x4 viewProj, Vector3 point)
        {
            var clip = viewProj * new Vector4(point.x, point.y, point.z, 1.0f);
            if (Mathf.Abs(clip.w) < 1e-5f)
            {
                return 0;
            }

            var ndc = clip / clip.w;
            return ndc.x >= -1.0f && ndc.x <= 1.0f &&
                   ndc.y >= -1.0f && ndc.y <= 1.0f &&
                   ndc.z >= 0.0f && ndc.z <= 1.0f
                ? 1
                : 0;
        }

        private static void GetViewSpaceBounds(
            Renderer[] casters,
            Bounds boardBounds,
            Matrix4x4 viewMatrix,
            out Vector3 min,
            out Vector3 max)
        {
            var firstCorner = viewMatrix.MultiplyPoint3x4(GetBoundsCorner(casters[0].bounds, 0));
            min = firstCorner;
            max = firstCorner;

            foreach (var renderer in casters)
            {
                EncapsulateBoundsCorners(renderer.bounds, viewMatrix, ref min, ref max);
            }

            EncapsulateBoundsCorners(boardBounds, viewMatrix, ref min, ref max);
        }

        private static void EncapsulateBoundsCorners(
            Bounds bounds,
            Matrix4x4 viewMatrix,
            ref Vector3 min,
            ref Vector3 max)
        {
            for (var i = 0; i < 8; i++)
            {
                var corner = viewMatrix.MultiplyPoint3x4(GetBoundsCorner(bounds, i));
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }
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
