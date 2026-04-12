using UnityEngine;

namespace Baku.VMagicMirror
{
    public static class VmmUrpPostProcessingRuntime
    {
        public static bool CropEnabled { get; set; }
        public static float CropMargin { get; set; } = 0.02f;
        public static float CropSquareRate { get; set; }
        public static float CropBorderWidth { get; set; } = 0.01f;
        public static Color CropBorderColor { get; set; } = Color.white;

        public static bool AlphaEdgeEnabled { get; set; }
        public static float AlphaEdgeThickness { get; set; } = 20f;
        public static float AlphaEdgeThreshold { get; set; } = 1f;
        public static Color AlphaEdgeColor { get; set; } = Color.white;
        public static float AlphaEdgeOutlineOverwriteAlpha { get; set; } = 0.02f;
        public static bool AlphaEdgeHighQualityMode { get; set; }

        public static bool RetroEffectsEnabled { get; set; }

        public static bool MonochromeUseBlock { get; set; }
        public static int MonochromeBlockSize { get; set; } = 4;
        public static bool MonochromeUseMonochrome { get; set; } = true;
        public static Color MonochromeBlack { get; set; } = new Color(
            r: 0.16470589f, g: 0.14117648f, b: 0.08627451f
            );
        public static Color MonochromeWhite { get; set; } = new Color(
            r: 0.9228164f, g: 0.941f, b: 0.7909855f);

        public static bool MonochromeUseLevel { get; set; } = true;
        public static int MonochromeLevelDivision { get; set; } = 8;
        public static int MonochromeWhiteThreshold { get; set; } = 4;
        public static bool MonochromeUseColorReduction { get; set; }
        public static int MonochromeColorDivision { get; set; } = 16;

        public static float VhsBleeding { get; set; } = 0.25f;
        public static float VhsFringing { get; set; } = 0.373f;
        public static float VhsScanline { get; set; } = 0.117f;

        public static bool HasAnyActiveEffect =>
            RetroEffectsEnabled || CropEnabled || AlphaEdgeEnabled;
    }
}
