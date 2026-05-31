using Baku.VMagicMirror.ExternalTracker;
using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror.Installer
{
    public class MonitoringInstaller : InstallerBase
    {
        [SerializeField] private GlobalHookInputChecker globalHookInputChecker = null;
        [SerializeField] private RawInputChecker robustRawInputChecker = null;
        [SerializeField] private MousePositionProvider mousePositionProvider = null;
        [SerializeField] private ExternalTrackerDataSource externalTracker = null;
        [SerializeField] private XInputGamePad gamepadListener = null;
        [SerializeField] private MidiInputObserver midiInputObserver = null;
        
        public override void Install(DiContainer container)
        {
            var isWindows = Application.platform == RuntimePlatform.WindowsEditor ||
                            Application.platform == RuntimePlatform.WindowsPlayer;

            //NOTE: 2つの実装が合体したキメラ実装を適用します。コレが比較的安全でいちばん動きも良いので。
            if (isWindows)
            {
                container.Bind<IKeyMouseEventSource>()
                    .FromInstance(new HybridInputChecker(robustRawInputChecker, globalHookInputChecker))
                    .AsCached();
            }
            else
            {
                // Linux fallback: avoid Windows raw input stack.
                if (robustRawInputChecker != null)
                {
                    robustRawInputChecker.enabled = false;
                }

                container.Bind<IKeyMouseEventSource>()
                    .FromInstance(globalHookInputChecker)
                    .AsCached();
            }
            container.BindInstance(mousePositionProvider);
            //container.BindInstance(faceTracker);
            
            container.Bind<FaceSwitchExtractor>().AsSingle();
            container.BindInstance(externalTracker);
            container.BindInterfacesAndSelfTo<FaceSwitchUpdater>().AsSingle();
            container.BindInstance(gamepadListener);
            container.BindInstance(midiInputObserver);
            container.BindInterfacesAndSelfTo<TrackingLostBlendShapeSource>().AsSingle();

            //終了前に監視処理を安全にストップさせたいものは呼んでおく
            if (isWindows)
            {
                container.Bind<IReleaseBeforeQuit>()
                    .FromInstance(robustRawInputChecker)
                    .AsCached();
            }

            container.Bind<IReleaseBeforeQuit>()
                .FromInstance(globalHookInputChecker)
                .AsCached();
            
            container.BindInterfacesAndSelfTo<HorizontalFlipController>().AsSingle();
        }
    }
}
