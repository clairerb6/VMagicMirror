using UniGLTF;
using UniVRM10;
using UnityEngine;

namespace Baku.VMagicMirror
{
    /// <summary>
    /// URP material descriptor generator that uses VMagicMirror's MToon10 shader variant.
    /// </summary>
    public sealed class VmmUrpVrm10MaterialDescriptorGenerator : IMaterialDescriptorGenerator
    {
        public VmmUrpVrm10MToonMaterialImporter MToonMaterialImporter { get; } = new();
        public BuiltInGltfUnlitMaterialImporter UnlitMaterialImporter { get; } = new();
        public UrpGltfPbrMaterialImporter PbrMaterialImporter { get; } = new();
        public UrpGltfDefaultMaterialImporter DefaultMaterialImporter { get; } = new();

        public MaterialDescriptor Get(GltfData data, int i)
        {
            if (MToonMaterialImporter.TryCreateParam(data, i, out var matDesc))
            {
                return matDesc;
            }

            if (UnlitMaterialImporter.TryCreateParam(data, i, out matDesc))
            {
                return matDesc;
            }

            if (PbrMaterialImporter.TryCreateParam(data, i, out matDesc))
            {
                return matDesc;
            }

            return GetGltfDefault(GltfMaterialImportUtils.ImportMaterialName(i, null));
        }

        public MaterialDescriptor GetGltfDefault(string materialName = null)
            => DefaultMaterialImporter.CreateParam(materialName);
    }

    /// <summary>
    /// MToon10 material importer that keeps UniVRM's MToon10 property mapping but swaps the shader.
    /// </summary>
    public sealed class VmmUrpVrm10MToonMaterialImporter
    {
        public const string ShaderName = "VMagicMirror/Universal Render Pipeline/MToon10 No Receive Shadow";

        private readonly UrpVrm10MToonMaterialImporter _baseImporter = new();

        public Shader Shader { get; set; }

        public VmmUrpVrm10MToonMaterialImporter(Shader shader = null)
        {
            Shader = shader != null ? shader : Shader.Find(ShaderName);
        }

        public bool TryCreateParam(GltfData data, int i, out MaterialDescriptor matDesc)
        {
            if (!_baseImporter.TryCreateParam(data, i, out var baseMatDesc))
            {
                matDesc = default;
                return false;
            }

            matDesc = new MaterialDescriptor(
                baseMatDesc.Name,
                Shader != null ? Shader : baseMatDesc.Shader,
                baseMatDesc.RenderQueue,
                baseMatDesc.TextureSlots,
                baseMatDesc.FloatValues,
                baseMatDesc.Colors,
                baseMatDesc.Vectors,
                baseMatDesc.Actions,
                baseMatDesc.AsyncActions);

            return true;
        }
    }
}
