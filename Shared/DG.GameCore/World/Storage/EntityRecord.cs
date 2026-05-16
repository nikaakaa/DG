using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal struct EntityRecord
{
    public EntityRecord(long entityId, GameEntity entity, int generation, bool alive, ulong componentMask)
    {
        EntityId = entityId;
        Entity = entity;
        Generation = generation;
        Alive = alive;
        ComponentMask = componentMask;
    }

    public long EntityId;
    public GameEntity Entity;
    public int Generation;
    public bool Alive;
    public ulong ComponentMask;
}

}
