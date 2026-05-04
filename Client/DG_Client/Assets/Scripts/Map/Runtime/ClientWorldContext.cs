using System;
using System.Collections.Generic;
using DG.GameCore;

namespace DG.Map
{
    public sealed class ClientWorldContext
    {
        private readonly List<MoveCommand> moveCommands = new();

        public ClientMapWorld ClientMapWorld { get; }
        public int TickIndex { get; private set; }
        public float TickTime { get; private set; }
        public float FixedDeltaTime { get; private set; }
        public int MoveCommandCount => moveCommands.Count;

        public ClientWorldContext(ClientMapWorld clientMapWorld)
        {
            ClientMapWorld = clientMapWorld ?? throw new ArgumentNullException(nameof(clientMapWorld));
        }

        public void AdvanceTick(float deltaTime)
        {
            FixedDeltaTime = deltaTime;
            TickTime += deltaTime;
            TickIndex++;
        }

        public void EnqueueMoveCommand(MoveCommand command)
        {
            moveCommands.Add(command);
        }

        public IReadOnlyList<MoveCommand> ConsumeMoveCommands()
        {
            MoveCommand[] consumed = moveCommands.ToArray();
            moveCommands.Clear();
            return consumed;
        }
    }
}
