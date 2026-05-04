using System.IO;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public static class ClientGameConfigProviderFactory
    {
        public static IGameConfigProvider Create()
        {
            string dataDirectory = Path.Combine(Application.streamingAssetsPath, "GameConfig");
            return LubanGameConfigProvider.FromDirectory(dataDirectory);
        }
    }
}
