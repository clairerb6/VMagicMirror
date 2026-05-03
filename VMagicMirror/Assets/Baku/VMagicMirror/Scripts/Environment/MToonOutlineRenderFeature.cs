using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    /// <summary>
    /// URP default passes do not render MToon's dedicated outline pass ("MToonOutline"),
    /// so we inject explicit render-object passes for both opaque and transparent queues.
    /// </summary>
    public sealed class VmmMToonOutlineRenderFeature : ScriptableRendererFeature
    {
        private static readonly string[] ShaderTags = { "MToonOutline" };

        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField] private RenderPassEvent opaqueEvent = RenderPassEvent.AfterRenderingOpaques;
        [SerializeField] private RenderPassEvent transparentEvent = RenderPassEvent.AfterRenderingTransparents;

        private RenderObjectsPass _opaquePass;
        private RenderObjectsPass _transparentPass;

        public override void Create()
        {
            var cameraSettings = new RenderObjects.CustomCameraSettings();
            _opaquePass = new RenderObjectsPass(
                "MToonOutline(Opaque)",
                opaqueEvent,
                ShaderTags,
                RenderQueueType.Opaque,
                layerMask,
                cameraSettings
            );

            _transparentPass = new RenderObjectsPass(
                "MToonOutline(Transparent)",
                transparentEvent,
                ShaderTags,
                RenderQueueType.Transparent,
                layerMask,
                cameraSettings
            );
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
            {
                return;
            }

            renderer.EnqueuePass(_opaquePass);
            renderer.EnqueuePass(_transparentPass);
        }
    }
}
