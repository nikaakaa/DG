## ADDED Requirements

### Requirement: World Composition Boundary
地图运行时 SHALL 将 `World` 暴露为门面，由它组合独立的 chunk storage、entity tracking、dirty tracking 和批量变更边界。

#### Scenario: World delegates chunk lookup
- **WHEN** 调用方通过 `World` 请求 chunk 或 cell
- **THEN** chunk 和 cell 查询由 chunk storage 职责处理
- **AND** 调用方获得与重构前一致的结果形态

#### Scenario: World delegates entity indexing
- **WHEN** 调用方通过 `World` 添加、移除、移动或查询 entity
- **THEN** entity index 由 entity tracking 职责处理
- **AND** `World` 围绕该操作协调 chunk 存在性和 dirty 标记

#### Scenario: World delegates dirty state
- **WHEN** 一个会改变 cell 状态的操作成功
- **THEN** dirty cells 和 dirty chunks 由 dirty tracking 职责记录

#### Scenario: World preserves service separation
- **WHEN** chunk storage、entity tracking 或 dirty tracking 内部实现变化
- **THEN** chunk storage 不直接拥有 entity index
- **AND** dirty tracking 不直接拥有 entity index

### Requirement: Chunk Storage Responsibility
地图运行时 SHALL 将 loaded chunk 所有权和 cell 查询与 entity index、dirty state 分离。

#### Scenario: Chunk creation
- **WHEN** 调用方通过 chunk storage 请求一个不存在的 chunk
- **THEN** 系统创建、存储并返回该 chunk

#### Scenario: Cell lookup
- **WHEN** 调用方按世界坐标请求 cell
- **THEN** 坐标被转换为 chunk 坐标和 local 坐标
- **AND** 当 chunk 已存在或由所请求 API 创建时，chunk storage 返回匹配的 cell

#### Scenario: Chunk range lookup
- **WHEN** 调用方用世界坐标和 chunk radius 请求周围 chunks
- **THEN** chunk storage 返回方形 chunk 范围内的所有 chunks
- **AND** 该范围内缺失的 chunks 由所请求 API 创建

### Requirement: Coordinate Key Boundary
地图运行时 SHALL 通过集中坐标服务生成 packed long chunk key 和 packed long cell key，而不是在调用点分散复制 key 计算逻辑。

#### Scenario: Chunk key generation
- **WHEN** chunk storage 或 entity tracking 需要索引 chunk
- **THEN** 它通过统一坐标服务获得 packed long chunk key

#### Scenario: Cell key generation
- **WHEN** dirty tracking 或 entity tracking 需要索引 cell
- **THEN** 它通过统一坐标服务获得 packed long cell key

#### Scenario: Negative coordinate conversion
- **WHEN** 世界坐标包含负数
- **THEN** chunk 坐标、local 坐标和 key 生成保持一致

#### Scenario: No new Vector2Int spatial keys
- **WHEN** 新增 chunk、cell、dirty 或 entity 空间索引
- **THEN** 它不新增 `Vector2Int` 字典 key

### Requirement: Entity Tracking Responsibility
地图运行时 SHALL 独立于 chunk/cell storage 追踪 entities。

#### Scenario: Entity registration
- **WHEN** 一个 entity 被注册到世界坐标
- **THEN** 它按 entity id 建立索引
- **AND** 它按 cell 坐标建立索引
- **AND** 它按 chunk 坐标建立索引

#### Scenario: Entity movement
- **WHEN** 已注册 entity 移动到新的世界坐标
- **THEN** entity 从旧 cell 和 chunk 索引中移除
- **AND** entity 被加入新的 cell 和 chunk 索引
- **AND** entity 坐标被更新

#### Scenario: Entity removal
- **WHEN** 已注册 entity 被移除
- **THEN** entity 从 id、cell、chunk 索引中移除

### Requirement: Entity Target Partitioning
地图运行时 SHALL 使用 `EntityTarget` enum 按 target/category 分区索引 entities，至少覆盖 all、player、monster、object。

#### Scenario: Register entity into target indexes
- **WHEN** 一个 player、monster 或 object entity 被注册
- **THEN** entity 被加入 all target 索引
- **AND** entity 被加入自身 category 对应的 target 索引

#### Scenario: Move entity between chunks
- **WHEN** 一个已注册 entity 移动到不同 chunk
- **THEN** all target 的 chunk 索引被更新
- **AND** entity 自身 category 的 chunk 索引被更新

#### Scenario: Query chunk by target
- **WHEN** 调用方查询某个 chunk 内的 player、monster 或 object target
- **THEN** entity tracker 直接从对应 target 的 chunk 索引返回结果
- **AND** 查询不需要遍历 all target 再按类型过滤

#### Scenario: Default all target query
- **WHEN** 调用方使用无 target entity 查询
- **THEN** 查询结果等价于 all target

### Requirement: Entity Range Query
地图运行时 SHALL 支持按世界坐标、范围和 target 查询附近 entities。

#### Scenario: Query nearby monsters
- **WHEN** 调用方请求某个世界坐标附近的 monster target
- **THEN** entity tracker 只从 monster target 的空间索引收集候选 entity

#### Scenario: Query nearby players
- **WHEN** 调用方请求某个世界坐标附近的 player target
- **THEN** entity tracker 只从 player target 的空间索引收集候选 entity

#### Scenario: Range query excludes unrelated targets
- **WHEN** 查询 target 为 object
- **THEN** player 和 monster entity 不出现在结果中

### Requirement: Observer Visibility Delta
地图运行时 SHALL 支持根据 observer 旧坐标、新坐标、cell 视野范围和 target 计算 entered/left entity 差量。

#### Scenario: Entity enters observer view
- **WHEN** observer 从旧坐标移动到新坐标
- **AND** 某个 target entity 出现在新视野范围内但不在旧视野范围内
- **THEN** visibility delta 的 entered 集合包含该 entity

#### Scenario: Entity leaves observer view
- **WHEN** observer 从旧坐标移动到新坐标
- **AND** 某个 target entity 出现在旧视野范围内但不在新视野范围内
- **THEN** visibility delta 的 left 集合包含该 entity

#### Scenario: Entity remains visible
- **WHEN** 某个 target entity 同时处于旧视野范围和新视野范围
- **THEN** visibility delta 不把该 entity 放入 entered 或 left

#### Scenario: Observer excluded from delta
- **WHEN** observer 自己也属于查询 target
- **THEN** visibility delta 不把 observer 自己放入 entered 或 left

#### Scenario: Delta does not depend on dirty state
- **WHEN** dirty tracker 中没有 changed cells 或 changed chunks
- **THEN** visibility delta 仍然可以根据 entity tracker 的空间索引计算 entered 和 left

### Requirement: Dirty Tracking Responsibility
地图运行时 SHALL 独立于 chunk storage 和 entity tracking 追踪 changed cells 与 changed chunks，并且 SHALL NOT 把 dirty state 作为 observer-specific 可见性同步的唯一来源。

#### Scenario: Mark changed cell
- **WHEN** 一个世界坐标被标记为 dirty
- **THEN** dirty tracker 将该世界坐标记录为 changed cell
- **AND** 将对应 chunk 坐标记录为 changed chunk

#### Scenario: Clear dirty state
- **WHEN** dirty state 被清理
- **THEN** changed cell 和 changed chunk 集合变为空

#### Scenario: Dirty state is global
- **WHEN** 一个 cell 被标记为 dirty
- **THEN** dirty tracker 只记录全局 changed cell/chunk
- **AND** dirty tracker 不计算任意 player 的 entered/left entity 集合

### Requirement: Cell Spatial Data
地图运行时 SHALL 保持 cell 为纯空间数据，而不是 entity。

#### Scenario: Cell stores coordinates
- **WHEN** chunk 创建或返回 cell
- **THEN** cell 存储世界坐标、chunk 坐标和 local 坐标
- **AND** cell 不参与 entity 注册

#### Scenario: Cell storage remains replaceable
- **WHEN** 后续把 chunk cell 存储从全量数组替换为懒分配或压缩存储
- **THEN** 调用方仍通过 chunk/chunk storage accessor 获取 cell
- **AND** entity tracking 不依赖 `Cell[,]` 作为索引来源

### Requirement: Batch Change Boundary
地图运行时 SHALL 为多个 cell 变更预留批量原子应用边界。

#### Scenario: Batch validates before apply
- **WHEN** 一个批量变更包含多个 cell 操作
- **THEN** 系统先验证整批操作是否可应用
- **AND** 验证失败时不应用任何部分变更

#### Scenario: Batch marks dirty after success
- **WHEN** 一个批量变更成功应用
- **THEN** 受影响的 cell 和 chunk 被记录到 dirty tracker

#### Scenario: Batch supports future inverse
- **WHEN** 后续需要撤销建造、拆除或结构变更
- **THEN** 批量变更边界允许记录 inverse/undo 所需的旧状态

### Requirement: Shared Map View Boundary
地图运行时 SHALL 避免把 chunk storage 与 entity tracking 强绑定为不可拆分对象，以便后续支持共享空间数据但独立 entity 视图。

#### Scenario: Shared chunk data extension
- **WHEN** 后续需要多个 world view 共享同一份 chunk 数据
- **THEN** 设计允许多个 entity tracker 引用同一类 chunk storage 边界

#### Scenario: Independent entity view extension
- **WHEN** 后续时间 tick 和操作 tick 需要不同 entity 子集
- **THEN** 设计允许 entity tracking 在不复制 chunk 数据的前提下独立扩展

### Requirement: Unity Async Convention
地图运行时 SHALL 避免在运行时异步流程中使用 Unity coroutine API。

#### Scenario: No coroutine APIs introduced
- **WHEN** 本底层重构被实施
- **THEN** 不引入新的 `StartCoroutine`、`StopCoroutine` 或 Unity coroutine `IEnumerator` 工作流

#### Scenario: Future async APIs
- **WHEN** 未来 chunk loading、streaming 或 generation API 需要异步执行
- **THEN** 它们使用基于 UniTask 的 API，而不是 Unity coroutine API
