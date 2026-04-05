using Newtonsoft.Json;
using System;

namespace Baku.VMagicMirrorConfig
{
    /// <summary>
    /// トラッキングロストしたときの表情の設定に関するクラス。
    /// <see cref="ExternalTrackerFaceSwitchItem"/> から不要なものを削ったような内容になっている
    /// </summary>
    public class TrackingLostFaceSwitchSetting
    {
        /// <summary> 条件に合致したとき動かすBlendShapeClipの名前 </summary>
        [JsonProperty("clipName")]
        public string ClipName { get; set; } = "";

        /// <summary>
        /// 条件を満たしているときのみ表示するアクセサリーがある場合、その名前。何もしない場合は空文字。
        /// </summary>
        [JsonProperty("accessoryName")]
        public string AccessoryName { get; set; } = "";

        public static TrackingLostFaceSwitchSetting FromJson(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    return new();
                }

                return JsonConvert.DeserializeObject<TrackingLostFaceSwitchSetting>(json) ?? new();
            }
            catch (Exception ex)
            {
                LogOutput.Instance.Write(ex);
                return new();
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this);
    }
}
