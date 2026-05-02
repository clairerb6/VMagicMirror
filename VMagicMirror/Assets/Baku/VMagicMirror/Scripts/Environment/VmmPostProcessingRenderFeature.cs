using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    public sealed class VmmPostProcessingRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private AvatarShadowMaskPass _maskPass;
        private VmmPostProcessingPass _pass;

        public override void Create()
        {
            _maskPass = new AvatarShadowMaskPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
            _pass = new VmmPostProcessingPass
            {
                renderPassEvent = passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var dropShadowVolume = VmmAvatarDropShadowVolume.GetActiveComponent();
            var dropShadowController = VmmAvatarDropShadowController.ActiveInstance;
            var hasDropShadow = dropShadowVolume != null &&
                dropShadowController != null &&
                dropShadowController.IsReady;
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                renderingData.cameraData.camera == null ||
                UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData) ||
                (!VmmUrpPostProcessingRuntime.HasAnyActiveEffect && !hasDropShadow))
            {
                return;
            }

            if (hasDropShadow)
            {
                _maskPass.Setup(dropShadowController);
                renderer.EnqueuePass(_maskPass);
            }

            _pass.Setup(dropShadowController);
            _pass.AdvanceFrameState();
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _maskPass?.Dispose();
            _pass?.Dispose();
        }

        private sealed class AvatarShadowMaskPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new("VmmAvatarShadowMask");

            private Material _avatarMaskMaterial;
            private VmmAvatarDropShadowController _controller;

            private sealed class PassData
            {
                public Renderer[] avatarRenderers;
                public Material avatarMaskMaterial;
                public int width;
                public int height;
            }

            public void Setup(VmmAvatarDropShadowController controller)
            {
                _controller = controller;
                EnsureMaterial();
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_avatarMaskMaterial);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_controller == null ||
                    !_controller.IsReady ||
                    _avatarMaskMaterial == null)
                {
                    return;
                }

                var maskColor = renderGraph.ImportTexture(_controller.AvatarMaskHandle);
                var maskDepth = renderGraph.ImportTexture(_controller.AvatarMaskDepthHandle);
                if (!maskColor.IsValid() || !maskDepth.IsValid())
                {
                    return;
                }

                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "Vmm Avatar Shadow Mask",
                    out var passData,
                    _profilingSampler);

                passData.avatarRenderers = _controller.AvatarRenderers;
                passData.avatarMaskMaterial = _avatarMaskMaterial;
                passData.width = _controller.AvatarMaskHandle.rt.width;
                passData.height = _controller.AvatarMaskHandle.rt.height;

                builder.SetRenderAttachment(maskColor, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(maskDepth, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetViewport(new Rect(0, 0, data.width, data.height));
                    context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1.0f, 0);

                    if (data.avatarRenderers == null)
                    {
                        return;
                    }

                    foreach (var renderer in data.avatarRenderers)
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
                            context.cmd.DrawRenderer(renderer, data.avatarMaskMaterial, subMeshIndex, 0);
                        }
                    }
                });
            }

            private void EnsureMaterial()
            {
                if (_avatarMaskMaterial != null)
                {
                    return;
                }

                var shader = Shader.Find("Hidden/Vmm/AvatarMaskCaster");
                if (shader != null)
                {
                    _avatarMaskMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
            }
        }

        private sealed class VmmPostProcessingPass : ScriptableRenderPass
        {
            private static readonly int CropMarginId = Shader.PropertyToID("_Margin");
            private static readonly int CropSquareRateId = Shader.PropertyToID("_SquareRate");
            private static readonly int CropBorderWidthId = Shader.PropertyToID("_BorderWidth");
            private static readonly int CropBorderColorId = Shader.PropertyToID("_BorderColor");

            private static readonly int AlphaEdgeThicknessId = Shader.PropertyToID("_Thickness");
            private static readonly int AlphaEdgeThresholdId = Shader.PropertyToID("_Threshold");
            private static readonly int AlphaEdgeColorId = Shader.PropertyToID("_EdgeColor");
            private static readonly int AlphaEdgeOutlineOverwriteAlphaId = Shader.PropertyToID("_OutlineOverwriteAlpha");
            private static readonly int AlphaEdgeHighQualityModeId = Shader.PropertyToID("_HighQualityMode");

            private static readonly int MonoUseMonochromeId = Shader.PropertyToID("_UseMonochrome");
            private static readonly int MonoDivisionId = Shader.PropertyToID("_Division");
            private static readonly int MonoWhiteThresholdId = Shader.PropertyToID("_WhiteThreshold");
            private static readonly int MonoUseLevelId = Shader.PropertyToID("_UseLevel");
            private static readonly int MonoBlackColorId = Shader.PropertyToID("_BlackColor");
            private static readonly int MonoWhiteColorId = Shader.PropertyToID("_WhiteColor");
            private static readonly int MonoBlockSizeId = Shader.PropertyToID("_BlockSize");
            private static readonly int MonoUseColorReductionId = Shader.PropertyToID("_UseColorReduction");
            private static readonly int MonoColorDivisionId = Shader.PropertyToID("_ColorDivision");

            private static readonly int VhsBleedTapsId = Shader.PropertyToID("_BleedTaps");
            private static readonly int VhsBleedDeltaId = Shader.PropertyToID("_BleedDelta");
            private static readonly int VhsFringeDeltaId = Shader.PropertyToID("_FringeDelta");
            private static readonly int VhsScanlineId = Shader.PropertyToID("_Scanline");
            private static readonly int VhsSrcId = Shader.PropertyToID("_src");
            private static readonly int VhsNoiseYId = Shader.PropertyToID("_NoiseY");

            private static readonly int ShadowOffsetId = Shader.PropertyToID("_ShadowOffset");
            private static readonly int ShadowScaleId = Shader.PropertyToID("_ShadowScale");
            private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
            private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
            private static readonly int AvatarMaskTexId = Shader.PropertyToID("_AvatarMaskTex");
            private static readonly int UseBackgroundPlaneId = Shader.PropertyToID("_UseBackgroundPlane");
            private static readonly int UseOpaqueBackgroundId = Shader.PropertyToID("_UseOpaqueBackground");
            private static readonly int BackgroundEyeDepthId = Shader.PropertyToID("_BackgroundEyeDepth");
            private static readonly int BackgroundDepthToleranceId = Shader.PropertyToID("_BackgroundDepthTolerance");

            private Material _cropMaterial;
            private Material _alphaEdgeMaterial;
            private Material _monochromeMaterial;
            private Material _vhsMaterial;
            private Material _dropShadowMaterial;
            private VmmAvatarDropShadowController _dropShadowController;

            private float _retroNoiseTimer;
            private float _retroNoiseResetThreshold;

            public VmmPostProcessingPass()
            {
                profilingSampler = new ProfilingSampler(nameof(VmmPostProcessingRenderFeature));
                ConfigureInput(ScriptableRenderPassInput.Depth);
                EnsureMaterials();
                ResetRetroNoiseCycle();
            }

            public void Setup(VmmAvatarDropShadowController controller)
            {
                _dropShadowController = controller;
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_cropMaterial);
                CoreUtils.Destroy(_alphaEdgeMaterial);
                CoreUtils.Destroy(_monochromeMaterial);
                CoreUtils.Destroy(_vhsMaterial);
                CoreUtils.Destroy(_dropShadowMaterial);
            }

            public void AdvanceFrameState()
            {
                _retroNoiseTimer += Time.deltaTime;
                if (_retroNoiseTimer > 1.0f && _retroNoiseTimer > _retroNoiseResetThreshold)
                {
                    ResetRetroNoiseCycle();
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                EnsureMaterials();
                var dropShadowVolume = VmmAvatarDropShadowVolume.GetActiveComponent();
                var hasRetro = VmmUrpPostProcessingRuntime.RetroEffectsEnabled;
                var hasCrop = VmmUrpPostProcessingRuntime.CropEnabled;
                var hasAlphaEdge = VmmUrpPostProcessingRuntime.AlphaEdgeEnabled;
                var hasDropShadow = dropShadowVolume != null &&
                    _dropShadowController != null &&
                    _dropShadowController.IsReady;

                if (!HasRequiredMaterials(hasRetro, hasCrop, hasAlphaEdge, hasDropShadow) ||
                    (!hasRetro && !hasCrop && !hasAlphaEdge && !hasDropShadow))
                {
                    return;
                }

                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.cameraColor.IsValid())
                {
                    return;
                }

                // activeColorTexture may already point at the backbuffer, which does not always expose a valid descriptor.
                // cameraColor is the stable intermediate target to clone descriptor settings from.
                var descriptor = resources.cameraColor.GetDescriptor(renderGraph);
                descriptor.name = "_VmmPostProcessingA";
                descriptor.clearBuffer = false;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;

                var tempA = renderGraph.CreateTexture(descriptor);
                descriptor.name = "_VmmPostProcessingB";
                var tempB = renderGraph.CreateTexture(descriptor);

                var source = resources.activeColorTexture;
                var destination = tempA;
                var used = false;

                void Apply(Material material, string passName)
                {
                    var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                    renderGraph.AddBlitPass(parameters, passName);
                    source = destination;
                    destination = destination == tempA ? tempB : tempA;
                    used = true;
                }

                if (hasRetro)
                {
                    UpdateMonochromeMaterial();
                    Apply(_monochromeMaterial, "Vmm Monochrome");

                    UpdateVhsMaterial(descriptor.width);
                    Apply(_vhsMaterial, "Vmm VHS");
                }

                if (hasCrop)
                {
                    if (hasDropShadow)
                    {
                        UpdateDropShadowMaterial(dropShadowVolume);
                        Apply(_dropShadowMaterial, "Vmm AvatarDropShadow");
                    }

                    UpdateCropMaterial();
                    Apply(_cropMaterial, "Vmm Crop");
                }
                else
                {
                    if (hasAlphaEdge)
                    {
                        UpdateAlphaEdgeMaterial();
                        Apply(_alphaEdgeMaterial, "Vmm AlphaEdge");
                    }

                    if (hasDropShadow)
                    {
                        UpdateDropShadowMaterial(dropShadowVolume);
                        Apply(_dropShadowMaterial, "Vmm AvatarDropShadow");
                    }
                }

                if (used)
                {
                    renderGraph.AddCopyPass(source, resources.activeColorTexture, "Vmm PostProcessing CopyBack");
                }
            }

            private void EnsureMaterials()
            {
                _cropMaterial ??= CreateMaterial("Hidden/Vmm/Crop");
                _alphaEdgeMaterial ??= CreateMaterial("Hidden/Vmm/AlphaEdge");
                _monochromeMaterial ??= CreateMaterial("Hidden/Vmm/Monochrome");
                _vhsMaterial ??= CreateMaterial("Hidden/Vmm/VHS");
                _dropShadowMaterial ??= CreateMaterial("Hidden/Vmm/AvatarDropShadow");
            }

            private static Material CreateMaterial(string shaderName)
            {
                var shader = Shader.Find(shaderName);
                return shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            }

            private bool HasRequiredMaterials(bool hasRetro, bool hasCrop, bool hasAlphaEdge, bool hasDropShadow) =>
                (!hasRetro || (_monochromeMaterial != null && _vhsMaterial != null)) &&
                (!hasCrop || _cropMaterial != null) &&
                (!hasAlphaEdge || _alphaEdgeMaterial != null) &&
                (!hasDropShadow || _dropShadowMaterial != null);

            private void UpdateCropMaterial()
            {
                _cropMaterial.SetFloat(CropMarginId, VmmUrpPostProcessingRuntime.CropMargin);
                _cropMaterial.SetFloat(CropSquareRateId, VmmUrpPostProcessingRuntime.CropSquareRate);
                _cropMaterial.SetFloat(CropBorderWidthId, VmmUrpPostProcessingRuntime.CropBorderWidth);
                _cropMaterial.SetColor(CropBorderColorId, VmmUrpPostProcessingRuntime.CropBorderColor);
            }

            private void UpdateAlphaEdgeMaterial()
            {
                _alphaEdgeMaterial.SetFloat(AlphaEdgeThicknessId, VmmUrpPostProcessingRuntime.AlphaEdgeThickness);
                _alphaEdgeMaterial.SetFloat(AlphaEdgeThresholdId, VmmUrpPostProcessingRuntime.AlphaEdgeThreshold);
                _alphaEdgeMaterial.SetColor(AlphaEdgeColorId, VmmUrpPostProcessingRuntime.AlphaEdgeColor);
                _alphaEdgeMaterial.SetFloat(AlphaEdgeOutlineOverwriteAlphaId, VmmUrpPostProcessingRuntime.AlphaEdgeOutlineOverwriteAlpha);
                _alphaEdgeMaterial.SetFloat(AlphaEdgeHighQualityModeId, VmmUrpPostProcessingRuntime.AlphaEdgeHighQualityMode ? 1f : 0f);
            }

            private void UpdateMonochromeMaterial()
            {
                _monochromeMaterial.SetFloat(MonoBlockSizeId, VmmUrpPostProcessingRuntime.MonochromeUseBlock
                    ? VmmUrpPostProcessingRuntime.MonochromeBlockSize
                    : 0f);
                _monochromeMaterial.SetFloat(MonoUseMonochromeId, VmmUrpPostProcessingRuntime.MonochromeUseMonochrome ? 1f : 0f);
                _monochromeMaterial.SetColor(MonoBlackColorId, VmmUrpPostProcessingRuntime.MonochromeBlack);
                _monochromeMaterial.SetColor(MonoWhiteColorId, VmmUrpPostProcessingRuntime.MonochromeWhite);
                _monochromeMaterial.SetFloat(MonoUseLevelId, VmmUrpPostProcessingRuntime.MonochromeUseLevel ? 1f : 0f);
                _monochromeMaterial.SetFloat(MonoDivisionId, VmmUrpPostProcessingRuntime.MonochromeLevelDivision);
                _monochromeMaterial.SetFloat(MonoWhiteThresholdId, VmmUrpPostProcessingRuntime.MonochromeWhiteThreshold);
                _monochromeMaterial.SetFloat(MonoUseColorReductionId, VmmUrpPostProcessingRuntime.MonochromeUseColorReduction ? 1f : 0f);
                _monochromeMaterial.SetFloat(MonoColorDivisionId, VmmUrpPostProcessingRuntime.MonochromeColorDivision);
            }

            private void UpdateVhsMaterial(int pixelWidth)
            {
                var bleedWidth = 0.04f * VmmUrpPostProcessingRuntime.VhsBleeding;
                var bleedStep = 2.5f / Mathf.Max(1, pixelWidth);
                var bleedTaps = Mathf.Max(1, Mathf.CeilToInt(bleedWidth / bleedStep));
                var bleedDelta = bleedWidth / bleedTaps;
                var fringeWidth = 0.0025f * VmmUrpPostProcessingRuntime.VhsFringing;

                _vhsMaterial.SetInt(VhsBleedTapsId, bleedTaps);
                _vhsMaterial.SetFloat(VhsBleedDeltaId, bleedDelta);
                _vhsMaterial.SetFloat(VhsFringeDeltaId, fringeWidth);
                _vhsMaterial.SetFloat(VhsScanlineId, VmmUrpPostProcessingRuntime.VhsScanline);
                _vhsMaterial.SetFloat(VhsSrcId, 0.5f);
                _vhsMaterial.SetFloat(VhsNoiseYId, 1.0f - _retroNoiseTimer);
            }

            private void UpdateDropShadowMaterial(VmmAvatarDropShadowVolume volume)
            {
                _dropShadowMaterial.SetVector(ShadowOffsetId, volume.offset.value);
                _dropShadowMaterial.SetVector(ShadowScaleId, volume.scale.value);
                _dropShadowMaterial.SetColor(ShadowColorId, volume.color.value);
                _dropShadowMaterial.SetFloat(AlphaThresholdId, volume.alphaThreshold.value);
                _dropShadowMaterial.SetTexture(AvatarMaskTexId, _dropShadowController.AvatarMaskHandle.rt);
                _dropShadowMaterial.SetFloat(UseBackgroundPlaneId, _dropShadowController.HasBackgroundImage ? 1f : 0f);
                _dropShadowMaterial.SetFloat(UseOpaqueBackgroundId, _dropShadowController.HasOpaqueCameraBackground ? 1f : 0f);
                _dropShadowMaterial.SetFloat(BackgroundEyeDepthId, _dropShadowController.BackgroundEyeDepth);
                _dropShadowMaterial.SetFloat(BackgroundDepthToleranceId, 0.75f);
            }

            private void ResetRetroNoiseCycle()
            {
                _retroNoiseTimer = 0f;
                _retroNoiseResetThreshold = UnityEngine.Random.Range(3f, 8f);
            }
        }
    }
}
