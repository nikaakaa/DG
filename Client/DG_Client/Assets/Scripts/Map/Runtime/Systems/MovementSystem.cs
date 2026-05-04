using System.Collections.Generic;
using DG.GameCore;

namespace DG.Map
{
    public sealed class MovementSystem : IClientSystem
    {
        private readonly MovementResolveSystem movementResolveSystem = new();

        public void Tick(ClientWorldContext context, float deltaTime)
        {
            IReadOnlyList<MoveCommand> commands = context.ConsumeMoveCommands();
            for (int i = 0; i < commands.Count; i++)
            {
                movementResolveSystem.Resolve(context.ClientMapWorld.CoreWorld, commands[i]);
            }
        }
    }
}
