using UnityEngine;

namespace Baku.VMagicMirror
{
    // DropShadow方式で実装した影のパラメータを設定するコンポーネント
    // 旧実装を削除しつつコードの経路だけ残すために作っているので、まだ実装はないし、全く別のクラスで値を受ける用に直すかも
    public sealed class AvatarDropShadowController : MonoBehaviour
    {
        public void SetEnabled(bool enable)
        {
        }

        // NOTE: as-isの初期値は 0.4f
        public void SetDepthOffset(float offset)
        {
        }

        public void SetShadowIntensity(float intensity)
        {
        }

        // NOTE: as-isの初期値は -20
        public void SetShadowYaw(int yawDeg)
        {
        }

        // NOTE: as-isの初期値は 8
        public void SetShadowPitch(int pitchDeg)
        {
        }
    }
}
