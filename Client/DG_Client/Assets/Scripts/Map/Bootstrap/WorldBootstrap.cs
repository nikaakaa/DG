using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [SerializeField] private bool useFallbackConfig;

        public ClientMapWorld ClientMapWorld { get; private set; }

        private void Awake()
        {
            EnsureWorld();
        }

        public ClientMapWorld EnsureWorld()
        {
            if (ClientMapWorld != null)
            {
                return ClientMapWorld;
            }

            ClientMapWorld = useFallbackConfig
                ? new ClientMapWorld(FallbackGameConfigProvider.Instance)
                : new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            return ClientMapWorld;
        }
    }
}
