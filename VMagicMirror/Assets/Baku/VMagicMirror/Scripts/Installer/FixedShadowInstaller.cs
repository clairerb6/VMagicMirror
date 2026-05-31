using UnityEngine;
using Zenject;

namespace Baku.VMagicMirror
{
    public class FixedShadowInstaller : MonoInstaller
    {
        [SerializeField] private Renderer fixedShadowBoard;

        public override void InstallBindings()
        {
            Container.BindInstance(fixedShadowBoard).WhenInjectedInto<FixedShadowController>();
            Container.BindInterfacesAndSelfTo<FixedShadowController>().AsSingle();
        }
    }
}
