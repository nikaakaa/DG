# Change: 重构地图运行时底层

## Why
当前地图运行时已经把 `Cell` 从 `Entity` 中拆出来，但 `World` 仍然直接持有 chunk 存储、实体流程编排和 dirty 状态。后续要加移动、建造、结构判定、地图流式加载和同步规则之前，需要先把底层职责拆清楚。

只拆出 chunk store 和 dirty tracker 还不够。参照 `Ref/Minestom` 后，当前提案必须补上两个会直接影响联机和玩法查询的底层能力：按实体类型分区的空间索引，以及按观察者视野计算 entity 进入/离开的差量。否则 dirty cell/chunk 只能说明“地图哪里变了”，不能说明“某个玩家应该收到什么变化”。

## What Changes
- 参照 `Ref/Minestom` 中 `Instance + Chunk + EntityTracker` 的分层，把地图运行时拆成更聚焦的底层服务。
- 保留 `World` 作为轻量门面，用来组合 chunk 存储、实体追踪、dirty 追踪和后续批量变更入口。
- 新增独立 chunk store，负责 chunk/cell 查询和创建。
- 新增独立 dirty tracker，只负责本地 cell/chunk 变更标记，不把它当作网络同步模型。
- 升级 `EntityTracker`，支持按 entity target/category 建立独立索引，至少覆盖 all、player、monster、object。
- 升级 `EntityTracker`，支持按观察者旧位置、新位置、cell 视野范围和 target 计算 entered/left 差量。
- 扩展坐标换算边界，集中管理 chunk/cell 坐标与稳定 key 的转换，为后续 packed long key 替换 `Vector2Int` 字典键留出口。
- 保持 `Cell` 为纯空间数据，`Chunk` 只负责空间存储，并明确空 chunk/cell 存储可以后续延迟分配或压缩。
- 预留批量 cell 变更的原子入口，用于后续建造、拆除、结构判定和撤销。
- 记录 SharedInstance 类似的“共享 chunk 数据、独立 entity 视图”方向，但本次不实现多世界视图。
- Unity 异步流程保持 UniTask 约定，不引入协程式 API。

## Impact
- Affected specs: map-runtime-foundation
- Affected code:
  - `Client/DG_Client/Assets/Scripts/Map/Spatial/World.cs`
  - `Client/DG_Client/Assets/Scripts/Map/Spatial/Chunk.cs`
  - `Client/DG_Client/Assets/Scripts/Map/Spatial/Cell.cs`
  - `Client/DG_Client/Assets/Scripts/Map/Spatial/MapCoordinate.cs`
  - `Client/DG_Client/Assets/Scripts/Map/Spatial/EntityTracker.cs`
  - new spatial services such as chunk storage, dirty tracking, entity target indexes, visibility delta helpers, and batch change boundaries
  - editor/debug checks that validate map indexing
