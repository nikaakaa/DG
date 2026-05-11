using System;
using DG.GameCore;

namespace DG.Map
{
    public sealed class ClientWorldContext
    {
        public ClientMapWorld ClientMapWorld { get; }
        public ClientAnimationLayer AnimationLayer { get; }
        public int TickIndex { get; private set; }
        public float TickTime { get; private set; }
        public float FixedDeltaTime { get; private set; }

        public ClientWorldContext(ClientMapWorld clientMapWorld)
            : this(clientMapWorld, ClientAnimationStyleProvider.Fallback())
        {
        }

        public ClientWorldContext(ClientMapWorld clientMapWorld, ClientAnimationStyleProvider animationStyleProvider)
        {
            ClientMapWorld = clientMapWorld ?? throw new ArgumentNullException(nameof(clientMapWorld));
            AnimationLayer = new ClientAnimationLayer(animationStyleProvider ?? ClientAnimationStyleProvider.Fallback());
        }

        public void AdvanceTick(float deltaTime)
        {
            FixedDeltaTime = deltaTime;
            TickTime += deltaTime;
            TickIndex++;
        }
    }
}
