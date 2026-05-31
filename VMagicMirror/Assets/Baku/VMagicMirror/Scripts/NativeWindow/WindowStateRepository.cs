using R3;

namespace Baku.VMagicMirror
{
    /// <summary>
    /// <see cref="WindowStyleController"/>でセットしたウィンドウのオンオフを他のクラスから使えるようにするクラス
    /// </summary>
    public sealed class WindowStateRepository
    {
        private readonly ReactiveProperty<bool> _windowVisible = new(true);
        public ReadOnlyReactiveProperty<bool> WindowVisible => _windowVisible;
        
        public void SetWindowVisible(bool visible)
        {
            _windowVisible.Value = visible;
        }
    }
}
