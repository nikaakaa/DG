## Context
当前地图运行时已经有 `World` 门面，并且具备 chunk storage、entity tracking、dirty tracking、范围查询和 visibility delta 能力。`WorldBootstrap` 会创建 `World`，但现在还没有一个运行时循环来统一决定玩法逻辑什么时候可以修改它。

下一个有价值的里程碑不是完整玩法框架，而是一个很小的推进器，用来证明这条链路：

`intent -> command buffer -> fixed tick -> system -> World.MoveEntity -> dirty state`

`Ref/Minestom` 可以作为架构参考，但它是 Java 服务端框架，不是 Unity 客户端运行时。这里借鉴它的推进形状和职责分层，不迁移它的服务端线程、网络和完整实体模拟。

## Goals
- 给客户端逻辑 tick 提供一个统一拥有者。
- 把直接修改 `World` 的行为收敛到被调度的系统里。
- 使用固定步长 tick，方便本地验证和后续网络扩展。
- 允许 EditMode 测试不进入 Play Mode 也能直接验证系统。
- 第一版保持本地单机逻辑。

## Non-Goals
- 不接入服务端权威或 Fantasy 协议。
- 不做预测、回滚、插值或回放。
- 不实现渲染刷新，只保留 dirty flush 边界。
- 不扩展寻路或碰撞规则。
- 不做 ECS/DOTS 转换。
- 不迁移 `ThreadDispatcher`、`Acquirable`、server scheduler、connection tick 或 packet flush。

## Decisions
- Decision: 参考 `Ref/Minestom` 的 tick 和 instance/world 分层，但只实现客户端最小版本。
- Rationale: Minestom 的 `TickSchedulerThread -> ServerProcess tick -> Instance.tick -> Chunk/Entity tick` 证明了固定 tick 和 world 归属边界的价值；Unity 客户端第一版只需要 `Update accumulator -> ClientWorldRunner -> client systems -> World`。

- Decision: 使用一个小型 `MonoBehaviour` runner 作为 Unity 入口。
- Rationale: 项目当前已经用 `WorldBootstrap` 作为场景桥接点，`MonoBehaviour` runner 是最快跑通客户端逻辑循环的方式。

- Decision: 使用固定逻辑 tick。
- Rationale: 移动和后续网络校正都需要稳定 tick 边界。第一版可以在 `Update` 中用 accumulator 累积时间。

- Decision: 修改 `World` 前先引入命令缓冲。
- Rationale: 外部输入、调试控制和未来网络消息都可以提交意图，但不拥有 world mutation 的时机。

- Decision: 系统保持为普通 C# 对象。
- Rationale: EditMode 测试可以直接构造 context、command buffer 和 systems，不需要完整场景。

- Decision: dirty flush 与 movement 分离。
- Rationale: dirty state 是来自 `map-runtime-foundation` 的全局地图状态信号；推进器需要让它在逻辑后可观察，但渲染仍然留到后续变更。

- Decision: 继续沿用 Minestom 风格的 target 索引、视野差量和 batch 边界思想。
- Rationale: 当前 `map-runtime-foundation` 已经把 `EntityTarget`、`DiffVisibility`、`CellBatchChange` 作为简化版边界落地；推进器应消费这些边界，而不是绕开它们直接操作底层索引。

## Minestom 借鉴边界
- 借鉴固定 tick，但在 Unity 主线程的 `MonoBehaviour.Update` 中用 accumulator 推进，不创建独立 tick thread。
- 借鉴 `Instance` 是世界逻辑归属点的思想，但映射为 `World + ClientWorldContext`，不引入服务端 InstanceManager。
- 借鉴 tick start / tick end 阶段，但第一版只落为确定系统顺序：movement 先执行，dirty flush 后执行。
- 借鉴 `EntityTracker.Target` 分区索引，但继续使用当前客户端的 `EntityTarget.All / Player / Monster / Object`。
- 借鉴 visibility difference，但继续使用当前 2D cell/chunk 范围，而不是 Minecraft 3D chunk/view distance。
- 借鉴 batch apply 的“先收集、后应用、统一通知”思想，但第一版只保留本地 cell batch 和 dirty 边界。
- 不借鉴 `ThreadDispatcher`、`Acquirable`、connection tick、packet flush、Minecraft 物理 tick、entity effect tick 或服务端事件总线。

## Sequencing
1. 新增运行时接口和 context。
2. 新增移动命令数据和命令缓冲。
3. 新增消费命令的 movement system。
4. 新增 dirty flush 边界。
5. 新增负责串联这些对象的 `MonoBehaviour` runner。
6. 新增 EditMode 测试，覆盖固定 tick、移动命令消费、dirty 捕获和 no-op tick。

## Risks / Trade-offs
- 如果 Unity 某一帧卡顿，固定 tick 可能在同一帧内执行多次。第一版实现需要限制或明确测试这个行为，再继续扩大运行时负载。
- 公开 context 可能慢慢变成杂物桶。第一版 context 字段只允许包含 world、tick 计数、时间和明确的命令缓冲。
- dirty flush 可能在渲染系统出现前过早清掉信号。第一版需要暴露 flush result 或 callback 边界，让测试能证明实际捕获了哪些 dirty 数据。

## Open Questions
- 第一阶段没有阻塞问题。本提案默认目标是本地单机推进器，用于验证最小 world mutation loop。
