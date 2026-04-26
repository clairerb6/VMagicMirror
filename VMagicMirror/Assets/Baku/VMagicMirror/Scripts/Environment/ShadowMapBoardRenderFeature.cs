using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    public sealed class ShadowMapBoardRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingOpaques;

        private ShadowMapBoardRenderPass _pass;

        public override void Create()
        {
            _pass = new ShadowMapBoardRenderPass
            {
                renderPassEvent = passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var controller = ShadowMapBoardController.ActiveInstance;
            if (controller == null ||
                !controller.IsReady ||
                renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            _pass.Setup(controller);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }

        private sealed class ShadowMapBoardRenderPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new(nameof(ShadowMapBoardRenderFeature));

            private Material _shadowDepthCasterMaterial;
            private ShadowMapBoardController _controller;

            private sealed class PassData
            {
                public Matrix4x4 shadowViewMatrix;
                public Matrix4x4 shadowProjectionMatrix;
                public Matrix4x4 cameraViewMatrix;
                public Matrix4x4 cameraProjectionMatrix;
                public Renderer[] shadowCasters;
                public Material shadowDepthCasterMaterial;
                public int shadowMapWidth;
                public int shadowMapHeight;
            }

            public void Setup(ShadowMapBoardController controller)
            {
                _controller = controller;
                EnsureMaterial();
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_shadowDepthCasterMaterial);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_controller == null ||
                    !_controller.IsReady ||
                    _shadowDepthCasterMaterial == null)
                {
                    return;
                }

                var shadowColor = renderGraph.ImportTexture(_controller.ShadowColorHandle);
                var shadowDepth = renderGraph.ImportTexture(_controller.ShadowDepthHandle);
                if (!shadowColor.IsValid() || !shadowDepth.IsValid())
                {
                    return;
                }

                var cameraData = frameData.Get<UniversalCameraData>();
                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    nameof(ShadowMapBoardRenderFeature),
                    out var passData,
                    _profilingSampler);

                passData.shadowViewMatrix = _controller.ShadowViewMatrix;
                passData.shadowProjectionMatrix = GL.GetGPUProjectionMatrix(_controller.ShadowProjectionMatrix, true);
                passData.cameraViewMatrix = cameraData.GetViewMatrix(0);
                passData.cameraProjectionMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(0), true);
                passData.shadowCasters = _controller.GetShadowCasters();
                passData.shadowDepthCasterMaterial = _shadowDepthCasterMaterial;
                passData.shadowMapWidth = _controller.ShadowColorHandle.rt.width;
                passData.shadowMapHeight = _controller.ShadowColorHandle.rt.height;

                builder.SetRenderAttachment(shadowColor, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(shadowDepth, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetViewport(new Rect(0, 0, data.shadowMapWidth, data.shadowMapHeight));
                    RenderingUtils.SetViewAndProjectionMatrices(
                        context.cmd,
                        data.shadowViewMatrix,
                        data.shadowProjectionMatrix,
                        false);
                    context.cmd.ClearRenderTarget(RTClearFlags.All, Color.white, 1.0f, 0);

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
                                context.cmd.DrawRenderer(renderer, data.shadowDepthCasterMaterial, subMeshIndex, 0);
                            }
                        }
                    }

                    RenderingUtils.SetViewAndProjectionMatrices(
                        context.cmd,
                        data.cameraViewMatrix,
                        data.cameraProjectionMatrix,
                        false);
                });
            }

            private void EnsureMaterial()
            {
                if (_shadowDepthCasterMaterial != null)
                {
                    return;
                }

                var shader = Shader.Find("Hidden/Vmm/ShadowMapDepthCaster");
                if (shader != null)
                {
                    _shadowDepthCasterMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
            }
        }
    }
}
