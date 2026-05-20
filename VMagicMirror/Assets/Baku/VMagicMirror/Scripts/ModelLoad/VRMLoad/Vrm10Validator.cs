using UniGLTF;

namespace Baku.VMagicMirror
{
    public static class Vrm10Validator
    {
        public static bool CheckModelIsVrm10(byte[] binary)
        {
            // NOTE: UniVRMの Vrm10Data と同じアプローチでVRM1かどうかを調べている
            using var gltfData = new GlbLowLevelParser("", binary).Parse();
            return UniGLTF.Extensions.VRMC_vrm.GltfDeserializer.TryGet(gltfData.GLTF.extensions, out _);
        }
    }
}
