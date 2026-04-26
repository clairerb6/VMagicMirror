using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    public sealed class ProjectedShadowRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingOpaques;

        private ProjectedShadowRenderPass _pass;

        public override void Create()
        {
            _pass = new ProjectedShadowRenderPass
            {
                renderPassEvent = passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var shadowController = ProjectedShadowController.ActiveInstance;
            if (shadowController == null ||
                !shadowController.IsReady ||
                renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            _pass.Setup(shadowController);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        private sealed class ProjectedShadowRenderPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new(nameof(ProjectedShadowRenderFeature));

            private Material _shadowCasterMaskMaterial;
            private ProjectedShadowController _shadowController;

            private sealed class PassData
            {
                public Matrix4x4 shadowViewMatrix;
                public Matrix4x4 shadowProjectionMatrix;
                public Matrix4x4 cameraViewMatrix;
                public Matrix4x4 cameraProjectionMatrix;
                public Renderer[] shadowCasters;
                public Material shadowCasterMaskMaterial;
                public int shadowTextureWidth;
                public int shadowTextureHeight;
            }

            public void Setup(ProjectedShadowController shadowController)
            {
                _shadowController = shadowController;
                EnsureMaterial();
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_shadowCasterMaskMaterial);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_shadowController == null ||
                    !_shadowController.IsReady ||
                    _shadowCasterMaskMaterial == null)
                {
                    return;
                }

                var importedShadowTexture = renderGraph.ImportTexture(_shadowController.ShadowTextureHandle);
                if (!importedShadowTexture.IsValid())
                {
                    return;
                }

                var universalRenderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                _ = universalRenderingData;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    nameof(ProjectedShadowRenderFeature),
                    out var passData,
                    _profilingSampler))
                {
                    passData.shadowViewMatrix = _shadowController.ShadowViewMatrix;
                    passData.shadowProjectionMatrix = GL.GetGPUProjectionMatrix(_shadowController.ShadowProjectionMatrix, true);
                    passData.cameraViewMatrix = cameraData.GetViewMatrix(0);
                    passData.cameraProjectionMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(0), true);
                    passData.shadowCasters = _shadowController.GetShadowCasters();
                    passData.shadowCasterMaskMaterial = _shadowCasterMaskMaterial;
                    passData.shadowTextureWidth = _shadowController.ShadowTexture.width;
                    passData.shadowTextureHeight = _shadowController.ShadowTexture.height;

                    builder.SetRenderAttachment(importedShadowTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetViewport(new Rect(0, 0, data.shadowTextureWidth, data.shadowTextureHeight));
                        context.cmd.SetViewProjectionMatrices(
                            data.shadowViewMatrix,
                            data.shadowProjectionMatrix);
                        RenderingUtils.SetViewAndProjectionMatrices(
                            context.cmd,
                            data.shadowViewMatrix,
                            data.shadowProjectionMatrix,
                            false);
                        context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1.0f, 0);
                        if (data.shadowCasters != null)
                        {
                            foreach (var renderer in data.shadowCasters)
                            {
                                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                                {
                                    continue;
                                }

                                var subMeshCount = renderer.sharedMaterials != null
                                    ? renderer.sharedMaterials.Length
                                    : 1;
                                for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                                {
                                    context.cmd.DrawRenderer(renderer, data.shadowCasterMaskMaterial, subMeshIndex, 0);
                                }
                            }
                        }
                        context.cmd.SetViewProjectionMatrices(
                            data.cameraViewMatrix,
                            data.cameraProjectionMatrix);
                        RenderingUtils.SetViewAndProjectionMatrices(
                            context.cmd,
                            data.cameraViewMatrix,
                            data.cameraProjectionMatrix,
                            false);
                    });
                }
            }

            private void EnsureMaterial()
            {
                if (_shadowCasterMaskMaterial != null)
                {
                    return;
                }

                var shader = Shader.Find("Hidden/Vmm/ShadowCasterMask");
                if (shader != null)
                {
                    _shadowCasterMaskMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
            }
        }
    }
}
