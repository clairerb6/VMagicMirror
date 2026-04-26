using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    public sealed class ProjectedShadowController : MonoBehaviour
    {
        private const string ProjectedShadowShaderName = "Custom/ProjectedShadowDrawer";

        private static readonly int ShadowTexId = Shader.PropertyToID("_ShadowTex");
        private static readonly int ShadowViewProjMatrixId = Shader.PropertyToID("_ShadowViewProjMatrix");

        public static ProjectedShadowController ActiveInstance { get; private set; }

        [SerializeField] private Light shadowLight = null;
        [SerializeField] private Renderer shadowBoardRenderer = null;
        [SerializeField] private int renderTextureSize = 1024;
        [SerializeField] private LayerMask shadowCasterLayerMask = ~0;
        [SerializeField] private Transform[] exclusionRoots = null;
        [SerializeField] private Vector3 projectionFocusOffset = new(0f, 0f, 0f);
        [SerializeField] private float projectionPadding = 0.15f;
        [SerializeField] private float maxCasterDistance = 8.0f;
        [SerializeField] private float nearClipPlane = 0.01f;
        [SerializeField] private float minFarClipPlane = 6.0f;
        [SerializeField] private float boardPlaneOffset = 0.02f;

        public LayerMask ShadowCasterLayerMask => shadowCasterLayerMask;
        public Matrix4x4 ShadowViewMatrix { get; private set; }
        public Matrix4x4 ShadowProjectionMatrix { get; private set; }
        public RenderTexture ShadowTexture => _shadowTexture;
        public RTHandle ShadowTextureHandle => _shadowTextureHandle;
        public bool IsReady =>
            isActiveAndEnabled &&
            shadowLight != null &&
            shadowBoardRenderer != null &&
            shadowBoardRenderer.gameObject.activeInHierarchy &&
            shadowBoardRenderer.enabled &&
            _shadowTexture != null &&
            _shadowTextureHandle != null;

        private RenderTexture _shadowTexture;
        private RTHandle _shadowTextureHandle;
        private MaterialPropertyBlock _propertyBlock;
        private Material _runtimeMaterial;
        private Renderer[] _shadowCasters = System.Array.Empty<Renderer>();
        private int _shadowCastersFrame = -1;
        private int _lastLoggedShadowCasterCount = -1;

        private void OnEnable()
        {
            ActiveInstance = this;
            _propertyBlock ??= new MaterialPropertyBlock();
            EnsureShadowTexture();
            EnsureProjectedShadowMaterial();
            UpdateShadowMatrices();
            ApplyMaterialProperties();
        }

        private void LateUpdate()
        {
            if (ActiveInstance != this)
            {
                ActiveInstance = this;
            }

            EnsureShadowTexture();
            EnsureProjectedShadowMaterial();
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
            ReleaseShadowTexture();
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public void EnsureShadowTexture()
        {
            var size = Mathf.Max(64, renderTextureSize);
            if (_shadowTexture != null &&
                _shadowTexture.width == size &&
                _shadowTexture.height == size)
            {
                return;
            }

            ReleaseShadowTexture();

            _shadowTexture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32)
            {
                name = "ProjectedShadowMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _shadowTexture.Create();
            _shadowTextureHandle = RTHandles.Alloc(_shadowTexture);
        }

        public Renderer[] GetShadowCasters()
        {
            if (_shadowCastersFrame == Time.frameCount)
            {
                return _shadowCasters;
            }

            _shadowCastersFrame = Time.frameCount;
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var casters = new System.Collections.Generic.List<Renderer>(allRenderers.Length);
            foreach (var renderer in allRenderers)
            {
                if (!IsValidShadowCaster(renderer))
                {
                    continue;
                }

                casters.Add(renderer);
            }

            _shadowCasters = casters.ToArray();
            if (_lastLoggedShadowCasterCount != _shadowCasters.Length)
            {
                _lastLoggedShadowCasterCount = _shadowCasters.Length;
                Debug.Log($"[ProjectedShadowController] shadow casters: {_shadowCasters.Length}");
            }
            return _shadowCasters;
        }

        private void ReleaseShadowTexture()
        {
            _shadowTextureHandle?.Release();
            _shadowTextureHandle = null;

            if (_shadowTexture == null)
            {
                return;
            }

            if (_shadowTexture.IsCreated())
            {
                _shadowTexture.Release();
            }
            Destroy(_shadowTexture);
            _shadowTexture = null;
        }

        private void EnsureProjectedShadowMaterial()
        {
            if (shadowBoardRenderer == null)
            {
                return;
            }

            if (_runtimeMaterial != null)
            {
                return;
            }

            var projectedShadowShader = Shader.Find(ProjectedShadowShaderName);
            if (projectedShadowShader == null)
            {
                return;
            }

            var sourceMaterial = shadowBoardRenderer.sharedMaterial;
            if (sourceMaterial != null)
            {
                _runtimeMaterial = new Material(sourceMaterial);
                _runtimeMaterial.name = sourceMaterial.name + " (ProjectedShadow)";
            }
            else
            {
                _runtimeMaterial = new Material(projectedShadowShader)
                {
                    name = "ProjectedShadowBoard"
                };
            }

            _runtimeMaterial.shader = projectedShadowShader;
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
                var fallbackFocus = shadowBoardRenderer.bounds.center + projectionFocusOffset;
                var fallbackPosition = fallbackFocus - shadowLight.transform.forward * 5.0f;
                ShadowViewMatrix = Matrix4x4.LookAt(fallbackPosition, fallbackFocus, shadowLight.transform.up);
                ShadowProjectionMatrix = Matrix4x4.Ortho(-2f, 2f, -2f, 2f, nearClipPlane, minFarClipPlane);
                return;
            }

            BuildShadowMatrices(casters, shadowLight.transform.forward, out var forwardView, out var forwardProj);
            BuildShadowMatrices(casters, -shadowLight.transform.forward, out var reverseView, out var reverseProj);

            var forwardScore = ScoreShadowMatrices(casters, forwardView, forwardProj);
            var reverseScore = ScoreShadowMatrices(casters, reverseView, reverseProj);

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
            if (shadowBoardRenderer == null || _shadowTexture == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            shadowBoardRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(ShadowTexId, _shadowTexture);
            _propertyBlock.SetMatrix(
                ShadowViewProjMatrixId,
                GL.GetGPUProjectionMatrix(ShadowProjectionMatrix, true) * ShadowViewMatrix
            );
            shadowBoardRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void BuildShadowMatrices(
            Renderer[] casters,
            Vector3 lookDirection,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix)
        {
            var forward = lookDirection.sqrMagnitude > 0.0f
                ? lookDirection.normalized
                : Vector3.forward;
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

            var boardCenter = shadowBoardRenderer.bounds.center;
            var focus = new Vector3(
                combinedBounds.center.x + projectionFocusOffset.x,
                boardCenter.y + boardPlaneOffset + projectionFocusOffset.y,
                combinedBounds.center.z + projectionFocusOffset.z
            );
            var position = focus - forward * maxCasterDistance;
            viewMatrix = Matrix4x4.LookAt(position, focus, up);

            GetViewSpaceBounds(casters, viewMatrix, out var min, out var max);

            var pullBack = Mathf.Max(0.0f, max.z + nearClipPlane + projectionPadding);
            if (pullBack > 0.0f)
            {
                position -= forward * pullBack;
                viewMatrix = Matrix4x4.LookAt(position, focus, up);
                GetViewSpaceBounds(casters, viewMatrix, out min, out max);
            }

            min -= Vector3.one * projectionPadding;
            max += Vector3.one * projectionPadding;

            projectionMatrix = Matrix4x4.Ortho(
                min.x,
                max.x,
                min.y,
                max.y,
                nearClipPlane,
                Mathf.Max(minFarClipPlane, -min.z)
            );
        }

        private int ScoreShadowMatrices(Renderer[] casters, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            var viewProj = GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            var score = 0;
            foreach (var renderer in casters)
            {
                var clip = viewProj * new Vector4(renderer.bounds.center.x, renderer.bounds.center.y, renderer.bounds.center.z, 1.0f);
                if (Mathf.Abs(clip.w) < 1e-5f)
                {
                    continue;
                }

                var ndc = clip / clip.w;
                if (ndc.x >= -1.0f && ndc.x <= 1.0f &&
                    ndc.y >= -1.0f && ndc.y <= 1.0f &&
                    ndc.z >= 0.0f && ndc.z <= 1.0f)
                {
                    score++;
                }
            }
            return score;
        }

        private static void GetViewSpaceBounds(Renderer[] casters, Matrix4x4 viewMatrix, out Vector3 min, out Vector3 max)
        {
            var firstCorner = viewMatrix.MultiplyPoint3x4(GetBoundsCorner(casters[0].bounds, 0));
            min = firstCorner;
            max = firstCorner;

            foreach (var renderer in casters)
            {
                var bounds = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = viewMatrix.MultiplyPoint3x4(GetBoundsCorner(bounds, i));
                    min = Vector3.Min(min, corner);
                    max = Vector3.Max(max, corner);
                }
            }
        }

        private static Vector3 GetBoundsCorner(Bounds bounds, int index)
        {
            var extents = bounds.extents;
            var center = bounds.center;
            return center + new Vector3(
                (index & 1) == 0 ? -extents.x : extents.x,
                (index & 2) == 0 ? -extents.y : extents.y,
                (index & 4) == 0 ? -extents.z : extents.z
            );
        }
    }
}
