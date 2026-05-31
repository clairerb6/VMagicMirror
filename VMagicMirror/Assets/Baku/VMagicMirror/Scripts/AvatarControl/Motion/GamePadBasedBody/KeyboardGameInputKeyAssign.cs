using System;

namespace Baku.VMagicMirror.GameInput
{
    [Serializable]
    public class KeyboardGameInputCustomAction
    {
        public GameInputCustomAction CustomAction;
        public string KeyCode;

        public string CustomActionKey => CustomAction?.CustomKey ?? "";
    }
    
    [Serializable]
    public class KeyboardGameInputKeyAssign
    {
        public bool UseMouseLookAround = true;
        public GameInputButtonAction LeftClick;
        public GameInputButtonAction RightClick;
        public GameInputButtonAction MiddleClick;

        public GameInputCustomAction CustomLeftClick;
        public GameInputCustomAction CustomRightClick;
        public GameInputCustomAction CustomMiddleClick;

        public string CustomLeftClickKey => CustomLeftClick?.CustomKey ?? "";
        public string CustomRightClickKey => CustomRightClick?.CustomKey ?? "";
        public string CustomMiddleClickKey => CustomMiddleClick?.CustomKey ?? "";

        //よくあるやつなので + このキーアサインでは補助キーを無視したいのでShiftも特別扱い
        public bool UseWasdMove = true;
        public bool UseArrowKeyMove = true;
        public bool UseShiftRun = true;
        public bool UseSpaceJump = true;

        //NOTE: ShiftとSpaceは上記のフラグで設定される場合、下記のキーコードで指定しなくても適用されるのがto-be
        //これは後方互換性、およびShiftキーの取り回しがちょっと面倒=KeysではLShiftとRShiftが別扱いされてるのが理由
        public string JumpKeyCode = "Space";
        public string RunKeyCode = "";
        public string CrouchKeyCode = "";

        public string TriggerKeyCode = "";
        public string PunchKeyCode = "";

        public KeyboardGameInputCustomAction[] CustomActions;

        public void OverwriteKeyCodeIntToKeyName()
        {
            JumpKeyCode = ParseIntToKeyName(JumpKeyCode);
            RunKeyCode = ParseIntToKeyName(RunKeyCode);
            CrouchKeyCode = ParseIntToKeyName(CrouchKeyCode);
            TriggerKeyCode = ParseIntToKeyName(TriggerKeyCode);
            PunchKeyCode = ParseIntToKeyName(PunchKeyCode);

            foreach (var ca in CustomActions)
            {
                ca.KeyCode = ParseIntToKeyName(ca.KeyCode);
            }
        }

        public static KeyboardGameInputKeyAssign LoadDefault() => new()
        {
            CustomActions = Array.Empty<KeyboardGameInputCustomAction>(),
        };

        private static string ParseIntToKeyName(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "";
            }

            if (!int.TryParse(key, out var value))
            {
                return "";
            }

            if (value >= 65 && value <= 90)
            {
                return ((char)value).ToString();
            }

            if (value >= 48 && value <= 57)
            {
                return $"D{value - 48}";
            }

            return value switch
            {
                13 => "Enter",
                16 => "ShiftKey",
                160 => "LShiftKey",
                161 => "RShiftKey",
                17 => "ControlKey",
                162 => "LControlKey",
                163 => "RControlKey",
                18 => "Menu",
                164 => "LMenu",
                165 => "RMenu",
                32 => "Space",
                37 => "Left",
                38 => "Up",
                39 => "Right",
                40 => "Down",
                45 => "Insert",
                46 => "Delete",
                35 => "End",
                36 => "Home",
                33 => "PageUp",
                34 => "PageDown",
                _ => value.ToString()
            };
        }
    }    
}
