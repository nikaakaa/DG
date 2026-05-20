using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class InspectRunner
    {
        public const string RunnerId = "inspect_runner";

        public RuntimeComponentInspectionResult Inspect(GameWorld world, long entityId)
        {
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                return RuntimeComponentInspectionResult.Empty(entityId);
            }

            GridCoord coord = world.TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
            Direction direction = world.TryGetComponent(entity, out DirectionComponent facing) ? facing.Direction : Direction.None;
            bool blocking = world.HasComponent<BlockingComponent>(entity);
            bool collider = world.HasComponent<ColliderComponent>(entity);
            IReadOnlyList<RuntimeEffectInstance> effects = FilterEffectsForEntity(world.RuntimeEffects.ActiveAt(world.ServerTick), entityId);
            return new RuntimeComponentInspectionResult(entityId, coord, direction, blocking, collider, effects);
        }

        private static IReadOnlyList<RuntimeEffectInstance> FilterEffectsForEntity(IReadOnlyList<RuntimeEffectInstance> source, long entityId)
        {
            var filtered = new List<RuntimeEffectInstance>();
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].TargetEntityId == entityId)
                {
                    filtered.Add(source[i]);
                }
            }

            return filtered;
        }
    }

    public readonly struct RuntimeComponentInspectionResult
    {
        public RuntimeComponentInspectionResult(long entityId, GridCoord coord, Direction direction, bool blocking, bool collider, IReadOnlyList<RuntimeEffectInstance> effects)
        {
            EntityId = entityId;
            Coord = coord;
            Direction = direction;
            Blocking = blocking;
            Collider = collider;
            Effects = effects ?? System.Array.Empty<RuntimeEffectInstance>();
        }

        public long EntityId { get; }
        public GridCoord Coord { get; }
        public Direction Direction { get; }
        public bool Blocking { get; }
        public bool Collider { get; }
        public IReadOnlyList<RuntimeEffectInstance> Effects { get; }

        public static RuntimeComponentInspectionResult Empty(long entityId) => new(entityId, default, Direction.None, false, false, System.Array.Empty<RuntimeEffectInstance>());
    }
}
