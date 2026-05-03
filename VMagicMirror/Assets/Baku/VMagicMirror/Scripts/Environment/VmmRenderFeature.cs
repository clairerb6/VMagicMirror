using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Baku.VMagicMirror
{
    public sealed class VmmRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

        private AvatarShadowMaskPass _maskPass;
        private VmmPostProcessingPass _pass;

        public override void Create()
        {
            _maskPass = new AvatarShadowMaskPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
            _pass = new VmmPostProcessingPass
            {
                renderPassEvent = passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var dropShadowController = VmmAvatarDropShadowController.ActiveInstance;
            var hasDropShadow = 
                dropShadowController != null &&
                dropShadowController.IsReady &&
                dropShadowController.HasAvatar;
            var hasPostProcess = VmmVolumeComponentAccessor.HasAnyActiveEffect();

            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                renderingData.cameraData.camera == null ||
                UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData) ||
                (!hasPostProcess && !hasDropShadow))
            {
                return;
            }

            if (hasDropShadow)
            {
                _maskPass.Setup(dropShadowController);
                renderer.EnqueuePass(_maskPass);
            }

            if (hasPostProcess)
            {
                _pass.AdvanceFrameState();
                renderer.EnqueuePass(_pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _maskPass?.Dispose();
            _pass?.Dispose();
        }

        private sealed class AvatarShadowMaskPass : ScriptableRenderPass
        {
            private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

            private readonly ProfilingSampler _profilingSampler = new("VmmAvatarShadowMask");

            private Shader _avatarMaskShader;
            private readonly Dictionary<int, Material> _avatarMaskMaterials = new();
            private VmmAvatarDropShadowController _controller;
            private Renderer[] _cachedAvatarRenderers;
            private DrawCommand[] _cachedDrawCommands;

            private sealed class DrawCommand
            {
                public Renderer Renderer;
                public Material Material;
                public int SubMeshIndex;
            }

            private sealed class PassData
            {
                public Matrix4x4 CameraViewMatrix;
                public Matrix4x4 CameraProjectionMatrix;
                public Matrix4x4 OverscannedProjectionMatrix;
                public DrawCommand[] DrawCommands;
                public int Width;
                public int Height;
            }

            public void Setup(VmmAvatarDropShadowController controller)
            {
                _controller = controller;
                EnsureMaterial();
            }

            public void Dispose()
            {
                DisposeMaskMaterials();
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_controller == null ||
                    !_controller.IsReady ||
                    _avatarMaskShader == null)
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

                var cameraData = frameData.Get<UniversalCameraData>();
                passData.DrawCommands = GetOrBuildDrawCommands(_controller.AvatarRenderers);
                passData.Width = _controller.AvatarMaskHandle.rt.width;
                passData.Height = _controller.AvatarMaskHandle.rt.height;
                passData.CameraViewMatrix = cameraData.GetViewMatrix(0);
                passData.CameraProjectionMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(0), true);
                passData.OverscannedProjectionMatrix = ComputeOverscannedProjectionMatrix(
                    cameraData.GetProjectionMatrix(0),
                    _controller.AvatarMaskOverscanFactor);

                builder.SetRenderAttachment(maskColor, 0);
                builder.SetRenderAttachmentDepth(maskDepth);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetViewport(new Rect(0, 0, data.Width, data.Height));
                    RenderingUtils.SetViewAndProjectionMatrices(
                        context.cmd,
                        data.CameraViewMatrix,
                        data.OverscannedProjectionMatrix,
                        false);
                    context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1.0f, 0);

                    if (data.DrawCommands == null)
                    {
                        return;
                    }

                    foreach (var drawCommand in data.DrawCommands)
                    {
                        var renderer = drawCommand.Renderer;
                        if (renderer == null || drawCommand.Material == null ||
                            !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        context.cmd.DrawRenderer(renderer, drawCommand.Material, drawCommand.SubMeshIndex, 0);
                    }

                    RenderingUtils.SetViewAndProjectionMatrices(
                        context.cmd,
                        data.CameraViewMatrix,
                        data.CameraProjectionMatrix,
                        false);
                });
            }

            private static Matrix4x4 ComputeOverscannedProjectionMatrix(Matrix4x4 projectionMatrix, float overscanFactor)
            {
                var overscannedProjectionMatrix = projectionMatrix;
                var safeOverscanFactor = Mathf.Max(1.0f, overscanFactor);
                overscannedProjectionMatrix.m00 /= safeOverscanFactor;
                overscannedProjectionMatrix.m11 /= safeOverscanFactor;
                return GL.GetGPUProjectionMatrix(overscannedProjectionMatrix, true);
            }

            private DrawCommand[] GetOrBuildDrawCommands(Renderer[] avatarRenderers)
            {
                if (ReferenceEquals(_cachedAvatarRenderers, avatarRenderers))
                {
                    return _cachedDrawCommands;
                }

                RebuildDrawCommands(avatarRenderers);
                return _cachedDrawCommands;
            }

            private void RebuildDrawCommands(Renderer[] avatarRenderers)
            {
                _cachedAvatarRenderers = avatarRenderers;
                _cachedDrawCommands = null;
                DisposeMaskMaterials();

                if (avatarRenderers == null || avatarRenderers.Length == 0)
                {
                    return;
                }

                var drawCommands = new List<DrawCommand>();
                foreach (var renderer in avatarRenderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var sharedMaterials = renderer.sharedMaterials;
                    var subMeshCount = sharedMaterials != null && sharedMaterials.Length > 0
                        ? sharedMaterials.Length
                        : 1;
                    for (var subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                    {
                        var sourceMaterial = sharedMaterials != null && subMeshIndex < sharedMaterials.Length
                            ? sharedMaterials[subMeshIndex]
                            : null;
                        var maskMaterial = GetOrCreateMaskMaterial(sourceMaterial);
                        if (maskMaterial == null)
                        {
                            continue;
                        }

                        drawCommands.Add(new DrawCommand
                        {
                            Renderer = renderer,
                            Material = maskMaterial,
                            SubMeshIndex = subMeshIndex
                        });
                    }
                }

                _cachedDrawCommands = drawCommands.ToArray();
            }

            private Material GetOrCreateMaskMaterial(Material sourceMaterial)
            {
                if (_avatarMaskShader == null)
                {
                    return null;
                }

                var key = sourceMaterial != null ? sourceMaterial.GetInstanceID() : 0;
                if (!_avatarMaskMaterials.TryGetValue(key, out var maskMaterial) || maskMaterial == null)
                {
                    maskMaterial = CoreUtils.CreateEngineMaterial(_avatarMaskShader);
                    if (maskMaterial == null)
                    {
                        return null;
                    }

                    maskMaterial.name = sourceMaterial != null
                        ? $"{sourceMaterial.name} (AvatarMask)"
                        : "AvatarMask (Default)";
                    _avatarMaskMaterials[key] = maskMaterial;
                    CopyMaskProperties(sourceMaterial, maskMaterial);
                }

                return maskMaterial;
            }

            private static void CopyMaskProperties(Material sourceMaterial, Material maskMaterial)
            {
                maskMaterial.SetTexture(BaseMapId, Texture2D.whiteTexture);
                maskMaterial.SetTextureScale(BaseMapId, Vector2.one);
                maskMaterial.SetTextureOffset(BaseMapId, Vector2.zero);
                maskMaterial.SetColor(BaseColorId, Color.white);

                maskMaterial.SetTexture(MainTexId, Texture2D.whiteTexture);
                maskMaterial.SetTextureScale(MainTexId, Vector2.one);
                maskMaterial.SetTextureOffset(MainTexId, Vector2.zero);
                maskMaterial.SetColor(ColorId, Color.white);

                maskMaterial.SetFloat(CutoffId, 0.5f);

                if (sourceMaterial == null)
                {
                    return;
                }

                if (sourceMaterial.HasProperty(BaseMapId))
                {
                    maskMaterial.SetTexture(BaseMapId, sourceMaterial.GetTexture(BaseMapId));
                    maskMaterial.SetTextureScale(BaseMapId, sourceMaterial.GetTextureScale(BaseMapId));
                    maskMaterial.SetTextureOffset(BaseMapId, sourceMaterial.GetTextureOffset(BaseMapId));
                }

                if (sourceMaterial.HasProperty(BaseColorId))
                {
                    maskMaterial.SetColor(BaseColorId, sourceMaterial.GetColor(BaseColorId));
                }

                if (sourceMaterial.HasProperty(MainTexId))
                {
                    maskMaterial.SetTexture(MainTexId, sourceMaterial.GetTexture(MainTexId));
                    maskMaterial.SetTextureScale(MainTexId, sourceMaterial.GetTextureScale(MainTexId));
                    maskMaterial.SetTextureOffset(MainTexId, sourceMaterial.GetTextureOffset(MainTexId));
                }

                if (sourceMaterial.HasProperty(ColorId))
                {
                    maskMaterial.SetColor(ColorId, sourceMaterial.GetColor(ColorId));
                }

                if (sourceMaterial.HasProperty(CutoffId))
                {
                    maskMaterial.SetFloat(CutoffId, sourceMaterial.GetFloat(CutoffId));
                }
            }

            private void DisposeMaskMaterials()
            {
                foreach (var material in _avatarMaskMaterials.Values)
                {
                    CoreUtils.Destroy(material);
                }

                _avatarMaskMaterials.Clear();
            }

            private void EnsureMaterial()
            {
                if (_avatarMaskShader != null)
                {
                    return;
                }

                _avatarMaskShader = Shader.Find("Hidden/Vmm/AvatarMaskCaster");
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

            private Material _cropMaterial;
            private Material _alphaEdgeMaterial;
            private Material _monochromeMaterial;
            private Material _vhsMaterial;

            private float _retroNoiseTimer;
            private float _retroNoiseResetThreshold;

            public VmmPostProcessingPass()
            {
                profilingSampler = new ProfilingSampler(nameof(VmmRenderFeature));
                EnsureMaterials();
                ResetRetroNoiseCycle();
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_cropMaterial);
                CoreUtils.Destroy(_alphaEdgeMaterial);
                CoreUtils.Destroy(_monochromeMaterial);
                CoreUtils.Destroy(_vhsMaterial);
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
                var cropVolume = VmmVolumeComponentAccessor.GetCropVolumeFromStack();
                var alphaEdgeVolume = VmmVolumeComponentAccessor.GetAlphaEdgeVolumeFromStack();
                var retroVolume = VmmVolumeComponentAccessor.GetRetroVolumeFromStack();
                var hasRetro = retroVolume.enabled.value;
                var hasCrop = cropVolume.enabled.value;
                var hasAlphaEdge = alphaEdgeVolume.enabled.value;

                if (!HasRequiredMaterials(hasRetro, hasCrop, hasAlphaEdge) ||
                    (!hasRetro && !hasCrop && !hasAlphaEdge))
                {
                    return;
                }

                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.cameraColor.IsValid())
                {
                    return;
                }

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

                if (hasRetro)
                {
                    UpdateMonochromeMaterial(retroVolume);
                    Apply(_monochromeMaterial, "Vmm Monochrome");

                    UpdateVhsMaterial(descriptor.width, retroVolume);
                    Apply(_vhsMaterial, "Vmm VHS");
                }

                if (hasCrop)
                {
                    UpdateCropMaterial(cropVolume);
                    Apply(_cropMaterial, "Vmm Crop");
                }
                else if (hasAlphaEdge)
                {
                    UpdateAlphaEdgeMaterial(alphaEdgeVolume);
                    Apply(_alphaEdgeMaterial, "Vmm AlphaEdge");
                }

                if (used)
                {
                    renderGraph.AddCopyPass(source, resources.activeColorTexture, "Vmm PostProcessing CopyBack");
                }

                return;

                void Apply(Material material, string blitPassName)
                {
                    var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                    renderGraph.AddBlitPass(parameters, blitPassName);
                    source = destination;
                    destination = destination == tempA ? tempB : tempA;
                    used = true;
                }
            }

            private void EnsureMaterials()
            {
                _cropMaterial ??= CreateMaterial("Hidden/Vmm/Crop");
                _alphaEdgeMaterial ??= CreateMaterial("Hidden/Vmm/AlphaEdge");
                _monochromeMaterial ??= CreateMaterial("Hidden/Vmm/Monochrome");
                _vhsMaterial ??= CreateMaterial("Hidden/Vmm/VHS");
            }

            private static Material CreateMaterial(string shaderName)
            {
                var shader = Shader.Find(shaderName);
                return shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            }

            private bool HasRequiredMaterials(bool hasRetro, bool hasCrop, bool hasAlphaEdge) =>
                (!hasRetro || (_monochromeMaterial != null && _vhsMaterial != null)) &&
                (!hasCrop || _cropMaterial != null) &&
                (!hasAlphaEdge || _alphaEdgeMaterial != null);

            private void UpdateCropMaterial(VmmCropVolume volume)
            {
                _cropMaterial.SetFloat(CropMarginId, volume.margin.value);
                _cropMaterial.SetFloat(CropSquareRateId, volume.squareRate.value);
                _cropMaterial.SetFloat(CropBorderWidthId, volume.borderWidth.value);
                _cropMaterial.SetColor(CropBorderColorId, volume.borderColor.value);
            }

            private void UpdateAlphaEdgeMaterial(VmmAlphaEdgeVolume volume)
            {
                _alphaEdgeMaterial.SetFloat(AlphaEdgeThicknessId, volume.thickness.value);
                _alphaEdgeMaterial.SetFloat(AlphaEdgeThresholdId, volume.threshold.value);
                _alphaEdgeMaterial.SetColor(AlphaEdgeColorId, volume.edgeColor.value);
                _alphaEdgeMaterial.SetFloat(AlphaEdgeOutlineOverwriteAlphaId, volume.outlineOverwriteAlpha.value);
                _alphaEdgeMaterial.SetFloat(AlphaEdgeHighQualityModeId, volume.highQualityMode.value ? 1f : 0f);
            }

            private void UpdateMonochromeMaterial(VmmRetroVolume volume)
            {
                _monochromeMaterial.SetFloat(MonoBlockSizeId, volume.useBlock.value
                    ? volume.blockSize.value
                    : 0f);
                _monochromeMaterial.SetFloat(MonoUseMonochromeId, volume.useMonochrome.value ? 1f : 0f);
                _monochromeMaterial.SetColor(MonoBlackColorId, volume.black.value);
                _monochromeMaterial.SetColor(MonoWhiteColorId, volume.white.value);
                _monochromeMaterial.SetFloat(MonoUseLevelId, volume.useLevel.value ? 1f : 0f);
                _monochromeMaterial.SetFloat(MonoDivisionId, volume.levelDivision.value);
                _monochromeMaterial.SetFloat(MonoWhiteThresholdId, volume.whiteThreshold.value);
                _monochromeMaterial.SetFloat(MonoUseColorReductionId, volume.useColorReduction.value ? 1f : 0f);
                _monochromeMaterial.SetFloat(MonoColorDivisionId, volume.colorDivision.value);
            }

            private void UpdateVhsMaterial(int pixelWidth, VmmRetroVolume volume)
            {
                var bleeding = volume.bleeding.value;
                var fringing = volume.fringing.value;
                var scanline = volume.scanline.value;
                var bleedWidth = 0.04f * bleeding;
                var bleedStep = 2.5f / Mathf.Max(1, pixelWidth);
                var bleedTaps = Mathf.Max(1, Mathf.CeilToInt(bleedWidth / bleedStep));
                var bleedDelta = bleedWidth / bleedTaps;
                var fringeWidth = 0.0025f * fringing;

                _vhsMaterial.SetInt(VhsBleedTapsId, bleedTaps);
                _vhsMaterial.SetFloat(VhsBleedDeltaId, bleedDelta);
                _vhsMaterial.SetFloat(VhsFringeDeltaId, fringeWidth);
                _vhsMaterial.SetFloat(VhsScanlineId, scanline);
                _vhsMaterial.SetFloat(VhsSrcId, 0.5f);
                _vhsMaterial.SetFloat(VhsNoiseYId, 1.0f - _retroNoiseTimer);
            }

            private void ResetRetroNoiseCycle()
            {
                _retroNoiseTimer = 0f;
                _retroNoiseResetThreshold = Random.Range(3f, 8f);
            }
        }
    }
}
