using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Baku.VMagicMirrorConfig.ViewModel
{
    /// <summary>
    /// トラッキングロスト時の表情操作の設定に関するビューモデル。
    /// <see cref="FaceSwitchItemViewModel"/> と似てるがThreshold設定やKeepLipSyncがなく、Localizeも決め打ちでいい点が異なる
    /// </summary>
    public class TrackingLostFaceSwitchViewModel : ViewModelBase
    {
        public TrackingLostFaceSwitchViewModel(FaceTrackerViewModel parent)
        {
            _parent = parent;

            if (!IsInDesignMode)
            {
                Setup();
                parent.SerializedTrackingLostFaceSwitchSetting.AddWeakEventHandler(
                    OnTrackingLostFaceSwitchSettingChanged
                    );

                // TODO: ほんとはClipNameが変わったらBlendShapeStoreにも反映したい
                ClipName.PropertyChanged += (s, e) => ApplyToModel();
                AccessoryName.PropertyChanged += (s, e) => ApplyToModel();

                // NOTE: これをやらないと「保存されたアクセサリー名」→「利用可能アクセサリー一覧」の順に取得したときにUIにうまく反映されない
                ((INotifyCollectionChanged)AvailableAccessoryNames).CollectionChanged += (s, e) =>
                {
                    RaisePropertyChanged(nameof(AccessoryName));
                };
            }
        }

        private readonly FaceTrackerViewModel _parent;

        private void OnTrackingLostFaceSwitchSettingChanged(object? sender, PropertyChangedEventArgs e) => Setup();

        private void Setup()
        {
            var setting = TrackingLostFaceSwitchSetting.FromJson(_parent.SerializedTrackingLostFaceSwitchSetting.Value);
            ClipName.Value = setting.ClipName;
            AccessoryName.Value = setting.AccessoryName;
        }

        private void ApplyToModel()
        {
            _parent.SerializedTrackingLostFaceSwitchSetting.Value = new TrackingLostFaceSwitchSetting()
            {
                ClipName = ClipName.Value,
                AccessoryName = AccessoryName.Value,
            }.ToJson();
        }


        public RProperty<bool> ShowAccessoryOption => _parent.ShowAccessoryOption;

        public ReadOnlyObservableCollection<string> BlendShapeNames => _parent.BlendShapeNames;

        public ReadOnlyObservableCollection<AccessoryItemNameViewModel> AvailableAccessoryNames 
            => _parent.AvailableAccessoryNames.Items;

        public RProperty<string> ClipName { get; } = new("");
        public RProperty<string> AccessoryName { get; } = new("");
    }
}
