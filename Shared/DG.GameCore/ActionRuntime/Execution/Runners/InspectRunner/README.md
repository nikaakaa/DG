# InspectRunner (RunnerId = `inspect_runner`)

实现 `debug_inspect` BehaviorId,负责把 entity 当前的组件 / 状态 / runtime effect 汇总成只读结果,供调试面板 (DGDebugPanel) 与 InspectorService 使用。

只读 runner:不产 `CommitProposal`,不改 world state,只走 component query。

## 状态枚举

```
Idle       -> 未启动
Collecting -> Enter:逐组件 query 当前 entity (position / direction / blocking / collider)
Filtering  -> Tick:从 RuntimeEffects.ActiveAt(serverTick) 过滤出 TargetEntityId 命中的 effect
Completed  -> 一帧结束 (CostTicks=1)
Failed     -> entity not found (返回 Empty 结果)
```

## 合法 transition

```
Idle       -> Collecting       (admission 通过)
Collecting -> Filtering        (entity 存在,基础组件查询完成)
Collecting -> Failed           ("entity not found")
Filtering  -> Completed
```

## Enter / Tick / Exit emit 的 fact

| Phase    | 必发 ActionFact   | 备注                                          |
|----------|-------------------|-----------------------------------------------|
| Enter    | (none)            |                                               |
| Tick     | (none)            |                                               |
| Exit     | (none)            | inspect 只读,不发 fact,只返回 result 结构    |

## 默认 claim

- channel: `debug`
- mode: `Shared` (只读,可与其他 Shared debug 并发)
- CostTicks: `1`

## Internal services

- `GameWorld.TryGetComponent<PositionComponent>` / `<DirectionComponent>`
- `GameWorld.HasComponent<BlockingComponent>` / `<ColliderComponent>`
- `GameWorld.RuntimeEffects.ActiveAt(tick)` + 手工 `TargetEntityId` 过滤

## 返回结构

`RuntimeComponentInspectionResult` (readonly struct):

| 字段        | 类型                                     | 说明                                  |
|-------------|------------------------------------------|---------------------------------------|
| EntityId    | `long`                                   | 被查 entity                           |
| Coord       | `GridCoord`                              | position 缺失时为 default             |
| Direction   | `Direction`                              | facing 缺失时为 `Direction.None`      |
| Blocking    | `bool`                                   | 是否含 BlockingComponent              |
| Collider    | `bool`                                   | 是否含 ColliderComponent              |
| Effects     | `IReadOnlyList<RuntimeEffectInstance>`   | 命中当前 serverTick 且 target = self  |

entity 不存在时返回 `RuntimeComponentInspectionResult.Empty(entityId)`。
