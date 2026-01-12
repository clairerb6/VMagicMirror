using uDesktopDuplication;
using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror
{
    public class DesktopLightEstimator : MonoBehaviour
    {
        //やや画面アス比をリスペクトしつつ、ピクセル数を大幅に絞っていく
        private const int Width = 32;
        private const int Height = 18;

        [SerializeField] private uDesktopDuplication.Texture ddTexture;
        // Unityのウィンドウが動いてなくともモニターのindexを再チェックする周期(sec)
        [SerializeField] private float desktopIndexCheckInterval = 10f;
        // Unityのウィンドウのサイズまたは位置が動いた場合にモニターのindexを再チェックする最短周期(sec)
        [SerializeField] private float desktopIndexCheckIntervalOnWindowMove = 1f;
        [SerializeField] private float textureReadInterval = 0.1f;
        [SerializeField] private float factorLerpFactor = 12f;
        [SerializeField] private ComputeShader colorMeanShader;

        public Vector3 RgbFactor { get; private set; } = Vector3.one;
        private Vector3 _rawFactor = Vector3.one;

        private RenderTexture _rt;
        private float _colorReadCount;
        private float _desktopIndexCheckTime;

        private int _colorMeanKernelIndex;
        private ComputeBuffer _colorMeanResultBuffer;
        private readonly float[] _colorMeanResult = new float[3];

        private bool _isEnabled = false;

        private NativeMethods.RECT? _prevWindowRect;
        
        public bool IsEnabled
        {
            get => _isEnabled;
            private set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                ddTexture.enabled = value;
                Manager.instance.enabled = value;

                if (value)
                {
                    _desktopIndexCheckTime = desktopIndexCheckInterval;
                }
                else
                {
                    RgbFactor = Vector3.one;
                    _rawFactor = Vector3.one;
                }
            }
        }

        [Inject]
        public void Initialize(IMessageReceiver receiver)
        {
            receiver.AssignCommandHandler(
                VmmCommands.UseDesktopLightAdjust,
                c => IsEnabled = c.ToBoolean()
            );
        }

        private void Start()
        {
            _rt = new RenderTexture(Width, Height, 32, RenderTextureFormat.BGRA32, 0);
            _colorMeanKernelIndex = colorMeanShader.FindKernel("CalcMeanColor");
            _colorMeanResultBuffer = new ComputeBuffer(3, sizeof(float));
            colorMeanShader.SetTexture(_colorMeanKernelIndex, "inputTexture", _rt);
            colorMeanShader.SetBuffer(_colorMeanKernelIndex, "resultColor", _colorMeanResultBuffer);
        }

        private void Update()
        {
            UpdateRawFactor();
            UpdateMonitorId();
            RgbFactor = IsEnabled
                ? Vector3.Lerp(RgbFactor, _rawFactor, factorLerpFactor * Time.deltaTime)
                : Vector3.one;
        }

        private void UpdateRawFactor()
        {
            if (!IsEnabled)
            {
                _colorReadCount = textureReadInterval;
                return;
            }

            if (ddTexture.monitor == null ||
                !ddTexture.monitor.exists ||
                ddTexture.monitor.state != DuplicatorState.Running ||
                ddTexture.monitor.texture == null)
            {
                return;
            }

            _colorReadCount += Time.deltaTime;
            if (_colorReadCount < textureReadInterval)
            {
                return;
            }

            _colorReadCount -= textureReadInterval;
            var source = ddTexture.monitor.texture;
            _rawFactor = GetColorByComputeShader(source);
        }

        private void UpdateMonitorId()
        {
            if (!IsEnabled)
            {
                return;
            }

            _desktopIndexCheckTime += Time.deltaTime;
            // 条件によらず最短周期は担保
            if (_desktopIndexCheckTime < desktopIndexCheckIntervalOnWindowMove)
            {
                return;
            }

            // デスクトップの情報が出揃ってないうちは待つ(数フレーム程度)
            // ここで待たされる間は結果的にデスクトップ0が参照される
            if (!CheckUDesktopDuplicationPrepared())
            {
                _desktopIndexCheckTime = 0f;
                ddTexture.monitorId = 0;
                return;
            }

            // 書いてる通りではあるが、
            // - シングルモニター環境では詳細チェックせず、単にプライマリモニターを使う
            // - Unityのウィンドウ位置が取れない場合も諦めてプライマリモニターを使う
            if (Manager.monitorCount <= 1 || !TryGetWindowRect(out var selfRect))
            {
                _desktopIndexCheckTime = 0f;
                ddTexture.monitorId = 0;
                return;
            }

            // - ウィンドウ位置が変わった (or 初取得): ただちに再チェック
            // - 十分な時間が経過した場合も再チェック
            if (!_prevWindowRect.HasValue || !_prevWindowRect.Value.Equals(selfRect) ||
                _desktopIndexCheckTime > desktopIndexCheckInterval)
            {
                _desktopIndexCheckTime = 0f;
                _prevWindowRect = selfRect;
                ddTexture.monitorId = CalculateMonitorId(selfRect);
            }
        }

        private Vector3 GetColorByComputeShader(Texture2D source)
        {
            //リサイズ
            Graphics.Blit(source, _rt);

            //リサイズしたテクスチャに対してGPUベースで色計算を行い、
            colorMeanShader.Dispatch(_colorMeanKernelIndex, 1, 1, 1);
            //CPUに引っ張り出す: このGetDataがちょっと重いことに留意すべし。
            _colorMeanResultBuffer.GetData(_colorMeanResult);

            var factor = new Vector3(_colorMeanResult[0], _colorMeanResult[1], _colorMeanResult[2]);
            return GetLightFactor(factor);
        }

        private static int CalculateMonitorId(NativeMethods.RECT selfRect)
        {
            var targetPos = GetTargetMonitorLeftTop(selfRect);

            var count = Manager.monitorCount;
            for (var i = 0; i < count; i++)
            {
                // NOTE: 万が一タイミングバグでidの範囲外になった場合はプライマリモニタが戻るので、そこまでケアしない
                var desktop = Manager.GetMonitor(i);
                if (desktop.left == targetPos.x && desktop.top == targetPos.y)
                {
                    return i;
                }
            }

            //何か検出に失敗した場合
            LogOutput.Instance.Write("failed to detect correct monitor about light...");
            return 0;
        }

        private static bool TryGetWindowRect(out NativeMethods.RECT rect)
        {
            if (NativeMethods.GetWindowRect(NativeMethods.GetUnityWindowHandle(), out rect))
            {
                return true;
            }

            rect = default;
            return false;
        }
        
        //uDesktopDuplicationで取得したいデスクトップの座標を、WinAPIから取得できるX,Y座標として取得する。
        private static Vector2Int GetTargetMonitorLeftTop(NativeMethods.RECT selfRect)
        {
            var monitorRects = NativeMethods.LoadAllMonitorRects();

            var selfCenter = new Vector2Int(
                (selfRect.left + selfRect.right) / 2, (selfRect.top + selfRect.bottom) / 2
            );

            //Unity画面の中央が入っているモニターがあれば、それで確定
            foreach (var monitor in monitorRects)
            {
                if (monitor.left <= selfCenter.x && selfCenter.x < monitor.right &&
                    monitor.top <= selfCenter.y && selfCenter.y < monitor.bottom)
                {
                    return new Vector2Int(monitor.left, monitor.top);
                }
            }

            //Unityウィンドウが縦長になって画面の下に潜った場合などで判定がうまく行かない場合、X座標のみで再判定
            foreach (var monitor in monitorRects)
            {
                if (monitor.left <= selfCenter.x && selfCenter.x < monitor.right)
                {
                    return new Vector2Int(monitor.left, monitor.top);
                }
            }

            //(かなり珍しいが)Unityウィンドウを全画面の右下とかに思い切り押し込んだ場合でも一応対応したいので、
            //ウィンドウの左上座標を便宜的に中心とみなして再判定
            selfCenter = new Vector2Int(selfRect.left, selfRect.top);
            foreach (var monitor in monitorRects)
            {
                if (monitor.left <= selfCenter.x && selfCenter.x < monitor.right &&
                    monitor.top <= selfCenter.y && selfCenter.y < monitor.bottom)
                {
                    return new Vector2Int(monitor.left, monitor.top);
                }
            }

            //全ての検出に失敗: 0,0を返すことで「プライマリモニタでお願いします」というニュアンスにする
            return Vector2Int.zero;
        }

        private static bool CheckUDesktopDuplicationPrepared() 
            => Manager.monitorCount == NativeMethods.LoadAllMonitorRects().Count;

        private static Vector3 GetLightFactor(Vector3 values)
        {
            //まず全ての色の値を引き上げ
            var r = LightFactorCurve(values.x);
            var g = LightFactorCurve(values.y);
            var b = LightFactorCurve(values.z);

            //輝度に応じて更に白を載せる。例えば黄色い背景の場合にbを足す
            var brightness = CalcBrightness(r, g, b) * 0.4f;

            return new Vector3(
                Mathf.Clamp01(r + brightness),
                Mathf.Clamp01(g + brightness),
                Mathf.Clamp01(b + brightness)
            );
        }

        private static float LightFactorCurve(float value)
        {
            //真っ暗にするのはあまり価値がないこと、および
            //そこそこ明るい場合は白に倒したいこと、および
            //アバター自身の映り込みによって黒方向に倒れやすいバイアスを消したいことなどを考慮したカーブです
            //x: 0.0 - 0.5 - 1.0
            //y: 0.1 - 1.0 - 1.0
            if (value > 0.5f)
            {
                return 1f;
            }
            else
            {
                return Mathf.Lerp(0.1f, 1f, value * 2f);
            }
        }

        private static float CalcBrightness(float r, float g, float b)
            => r * 0.3f + g * 0.59f + b * 0.11f;
    }
}