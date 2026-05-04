## Context
本项目是一个 2D 格子式多人地图原型。目标方向是 ECS-like：`Cell` 是空间数据，`Entity` 是动态玩法对象，规则由 system/service 处理。

`Ref/Minestom` 提供了有价值的边界参考：
- `Instance` 作为世界门面。
- `Chunk` 存储空间/方块数据。
- `EntityTracker` 独立于 block/chunk 存储来追踪实体。
- `EntityTracker.Target` 为不同实体类型维护独立索引。
- `EntityTracker.Update` 和 `difference` 在 entity 移动时计算视野 entered/left 差量。
- `CoordConversion.chunkIndex` 把两个 chunk 坐标压成稳定 key。
- `Palette` 用 single value、indirect、direct 模式避免空 chunk 大量分配。
- `AbsoluteBlockBatch` 把多个 block 变更按 chunk 分组并原子应用。
- `SharedInstance` 共享 chunk 数据，但拥有独立 entity tracker。

当前代码已经有 `World`、`Chunk`、`Cell`、`MapCoordinate` 和 `EntityTracker`，但 `World` 仍然直接持有 chunk 字典和 dirty 集合。当前 `EntityTracker` 也把所有 entity 混在同一套 cell/chunk 索引中，后续“查附近怪物”“查格子物体”“查附近玩家”会变成先取全量再过滤。

更关键的是，dirty cell/chunk 不能承担联机同步语义。它只能表示空间发生变化，不能表示某个玩家视野里有哪些 entity 进入或离开。玩家周围 16x16 同步需要的是 observer-specific visibility delta，而不是全局 dirty set。

## Goals
- 在玩法系统加入前，把地图底层边界拆清楚并稳定下来。
- 让 `World` 保持轻量门面形态。
- 分离空间存储、实体追踪、dirty 追踪。
- 定义 chunk 创建、cell 查询、实体添加/移动/移除、dirty 收集的第一版行为。
- 为玩家、怪物、物体等 target/category 建立独立实体空间索引。
- 为观察者视野同步提供 entered/left 差量计算边界。
- 让 dirty tracker 明确服务本地刷新和调试，不承担玩家视野同步职责。
- 集中坐标转换和 key 生成，允许后续从 `Vector2Int` key 迁移到 packed long key。
- 让 chunk/cell 存储边界允许后续空 chunk 延迟分配或压缩存储。
- 为后续建造、拆除、结构判定预留批量原子变更入口。
- 保持后续迁移 ECS/DOTS 的可能性。
- 未来 Unity 运行时异步 API 使用 UniTask，避免协程 API。

## Non-Goals
- 本次不实现具体移动规则、阻挡规则、放置规则、推动、流水线、结构判定、战斗、真实网络发送或 chunk 流式加载。
- 本次不把项目转换成 Unity DOTS/ECS。
- 本次不实现完整 palette 压缩，只要求 chunk/cell API 不锁死为永远全量 `Cell[,]`。
- 本次不实现完整 batch undo，只预留批量变更的原子边界。
- 本次不实现 SharedInstance 或多世界视图，只记录共享 chunk 数据、独立 entity tracker 的后续方向。
- 本次不引入 tag/组件映射体系，entity target 先使用 enum。
- 本次不加入持久化或程序化地图生成。
- 本次不引入协程式 API。

## Decisions
- Decision: 引入 chunk store 服务。
  - Reason: Minestom 将 chunk 所有权与实体追踪分离。当前 `World` 中的 chunk 字典应变成明确的存储服务，负责 chunk/cell 查询和创建。

- Decision: 引入 dirty tracker 服务，但收窄职责。
  - Reason: dirty cells 和 dirty chunks 适合服务渲染刷新、debug window 和本地局部重建，不适合作为联机同步模型。联机同步应基于观察者视野差量。

- Decision: 保留 `World` 作为门面。
  - Reason: 现有调用点已经依赖 `World`；保留门面可以避免大范围 API 震荡，同时让内部服务边界更清晰。

- Decision: 保持 `EntityTracker` 独立于 chunk。
  - Reason: 这对应 Minestom 的 `EntityTracker` 分离方式，也能让 `Chunk` 保持纯空间存储。

- Decision: 为 `EntityTracker` 增加 target/category 分区索引。
  - Reason: 玩家、怪物、物体是玩法上不同的查询目标。按 target 单独维护 cell/chunk 索引，能避免高频查询每次遍历混合 entity 列表再过滤。

- Decision: entity target 第一阶段使用 enum，最小集合为 all、player、monster、object。
  - Reason: 项目刚开始，不需要背兼容包袱，也不需要提前引入 tag/组件映射。enum 足够直接，后续 tag 系统出现后再把 enum 解析入口替换成 tag 或组件映射。

- Decision: 为 `EntityTracker` 增加 visibility delta 查询。
  - Reason: 玩家移动或观察范围变化时，需要计算 entered 和 left entities。dirty tracker 只能表示全局变更，不知道对哪个 observer 脏。

- Decision: visibility radius 对外使用 cell 范围表达。
  - Reason: 策划里的“周围 16x16”是格子语义。内部可以把 cell 范围换算成 chunk 范围来收集候选 entity，但上层规则不直接暴露 chunk radius。

- Decision: 坐标 key 第一阶段直接使用 packed long。
  - Reason: 项目刚开始，不需要先保留 `Vector2Int` 字典 key。`MapCoordinate.ToChunkKey` 和 `MapCoordinate.ToCellKey` 直接返回 long，可以尽早避免 key 分配和迁移成本。

- Decision: chunk/cell 存储不再把全量 `Cell[,]` 作为永久约束。
  - Reason: 当前 32x32 chunk 会无条件创建 1024 个 `Cell` 对象。Palette 的 single value 思路提示我们，空 chunk 可以只表示默认状态，只有访问或修改时再 materialize cell 数据。

- Decision: `Chunk.Cells` 不作为公开兼容视图保留，新代码只通过 accessor 获取 cell。
  - Reason: 项目刚开始，不需要围绕 `Cell[,]` 做兼容。通过 `GetCell`、`TryGetCell` 或等价 accessor 暴露 cell，后续才能替换成懒分配或压缩存储。

- Decision: 批量变更作为边界进入本次设计。
  - Reason: 建造、拆除和结构判定可能涉及多个相邻格子。后续应该通过 batch 先验证再应用，避免半成功状态。本次只保留入口和任务拆分，不实现完整玩法规则或 undo。

- Decision: batch 第一阶段只处理 cell 变更。
  - Reason: entity add/move/remove 已经会牵动 target 索引和 visibility delta。第一阶段让 batch 专注 cell 变更，可以先把建造、拆除、结构占格的原子边界定稳。

- Decision: SharedInstance 思路只作为未来扩展约束。
  - Reason: 双 tick 或多视图可能需要共享地图空间数据但使用独立 entity tracker。本次底层服务应避免把 chunk store 和 entity tracker 强绑成不可分割对象。

- Decision: 保持 `Cell` 为纯数据。
  - Reason: cell 表示空间，不是玩法对象。玩家、怪物、放置物等动态对象继续作为 entity。

- Decision: 明确异步约束，但暂不新增异步 API。
  - Reason: 当前还没有 chunk streaming。未来引入运行时异步 API 时，应使用 UniTask 和 cancellation token，而不是 Unity coroutine。

## Proposed Shape
```text
World
  ChunkStore
  EntityTracker
  DirtyTracker
  BatchChange entrypoint

ChunkStore
  LoadedChunks
  GetOrCreateChunk
  TryGetChunk
  HasChunk
  GetOrCreateCell
  TryGetCell
  GetChunksAround

MapCoordinate
  ToChunkCoord
  ToLocalCoord
  ToWorldCoord
  ToChunkKey
  ToCellKey

EntityTracker
  RegisteredEntities
  Register(entity, coord, target)
  Unregister(entity)
  Move(entity, coord)
  TryGetEntity
  GetEntitiesAt(coord, target)
  TryGetChunkEntities(chunkCoord, target)
  GetEntitiesAround(coord, radius, target)
  DiffVisibility(oldCoord, newCoord, radius, target)

EntityTarget
  All
  Player
  Monster
  Object

VisibilityDelta
  Entered
  Left

DirtyTracker
  ChangedCells
  ChangedChunks
  MarkCell
  MarkChunk
  MarkCellAndChunk
  Clear

Chunk
  Coord
  cell storage boundary

Cell
  Coord
  ChunkCoord
  LocalCoord
```

## Risks / Trade-offs
- Risk: 过早拆分可能产生空壳包装。
  - Mitigation: 只拆当前确实拥有状态或马上会约束后续架构的服务：chunk storage、dirty tracking、typed entity indexing、visibility delta、coordinate keys。

- Risk: target/category 设计过早固化实体分类。
  - Mitigation: 先使用最小集合：all、player、monster、object。后续新增 target 不应影响 chunk store 和 dirty tracker。

- Risk: visibility delta 容易被误解成已经完成网络同步。
  - Mitigation: 明确本次只提供“对 observer 的 entered/left 计算”，不负责协议、序列化、可靠发送或客户端状态合并。

- Risk: packed key 立即替换可能导致 API 震荡。
  - Mitigation: 本次先集中 key 生成入口，是否内部改用 long 由实现阶段按风险决定。

- Risk: chunk/cell 懒分配会要求 debug window 改为 accessor 读取。
  - Mitigation: 项目刚开始，优先让 debug window 跟随新 accessor，不再围绕 `Cell[,]` 做兼容设计。

- Risk: 批量变更边界可能扩张成建造系统。
  - Mitigation: 本次只实现或预留基础 batch apply 形状，不写放置规则、结构规则或 undo 玩法。

- Risk: 如果具体集合泄漏，外部可能修改内部状态。
  - Mitigation: 对外暴露只读接口，内部可变集合保持 private。

- Risk: 未来异步 chunk loading 可能要求 API 调整。
  - Mitigation: 本次保持同步实现，但保留 UniTask 作为后续异步 API 的命名和实现约定。

## Migration Plan
1. 新增 `ChunkStore`，将 chunk/cell 查询和创建迁入其中。
2. 新增或扩展 `MapCoordinate` key 生成入口，让 chunk/cell key 直接返回 packed long。
3. 新增 `DirtyTracker`，将 dirty cell/chunk 状态迁入其中，并收窄为本地变更追踪。
4. 定义 `EntityTarget` enum，先覆盖 all、player、monster、object。
5. 扩展 `EntityTracker`，将 id、cell、chunk 索引升级为按 target 分区维护。
6. 增加 target 查询 API：按 cell、chunk、范围查询目标 entity。
7. 增加 visibility delta API：根据旧位置、新位置、cell 范围和 target 计算 entered/left。
8. 更新 `World`，让它委托这些服务完成底层工作。
9. 为 cell batch 变更入口建立最小边界，避免未来建造系统直接散落调用多个单 cell API。
10. 验证 editor debug 检查和项目编译。

## Resolved Choices
- entity target 第一阶段使用 enum，不引入 tag/组件映射。
- target 最小集合为 all、player、monster、object。
- visibility radius 对外按 cell 范围表达，内部可以换算 chunk 范围做候选收集。
- `MapCoordinate.ToChunkKey` 和 `MapCoordinate.ToCellKey` 第一阶段直接返回 packed long。
- `Chunk.Cells` 不作为公开兼容视图保留，新代码通过 accessor 获取 cell。
- batch 入口第一阶段只支持 cell 变更，entity add/move/remove 继续走 `World` 和 `EntityTracker`。
