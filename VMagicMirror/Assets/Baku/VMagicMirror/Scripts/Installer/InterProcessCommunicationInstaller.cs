using Baku.VMagicMirror.InterProcess;
using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror.Installer
{
    /// <summary>
    /// プロセス間通信のインフラをInstallする処理
    /// </summary>
    public class InterProcessCommunicationInstaller : InstallerBase
    {
        
        public override void Install(DiContainer container)
        {
            var useTcpTransport = Application.platform == RuntimePlatform.LinuxPlayer ||
                                  Application.platform == RuntimePlatform.LinuxEditor;

            container
                .Bind<IIpcTransport>()
                .To(useTcpTransport ? typeof(TcpIpcTransport) : typeof(MmfIpcTransport))
                .AsCached();

            container
                .BindInterfacesTo<MmfBasedMessageIo>()
                .AsCached();

            container
                .Bind<ErrorIndicateSender>()
                .AsCached();

            container
                .Bind<ErrorInfoFactory>()
                .AsCached();
        }
    }
}
