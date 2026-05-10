## Context

当前 push 传播使用 `DeferredAction` 表达下一 tick 的结构化输出。一个 blocked push action 会读取 contact，解析 downstream subject，然后产生 deferred output。`AuthoritativeWorldTickRunner` 将这些 deferred output 重新放回 `WorldActionQueue`，到 `readyTick` 再进入普通 action pipeline。

手动日志显示，复杂结构会让多个传播分支在同一 tick 汇合到同一目标，例如同一 tick 内反复出现：

```text
player_push entity:800000148 dir:Up
player_push entity:800000149 dir:Up
```

这些 action 的 subject、direction、ready tick 等价，但 parent causality 不同，因此当前 `dedupeKey` 不同，无法合并。结果是队列数量按分支数膨胀，日志和 CPU 压力迅速上升。

## Goals

- 阻止同 tick 等价 deferred push 重复入队。
- 保持局部 deferred 传播模型，不回到同 tick 全链求解。
- 保持不同方向 deferred 共存，为后续 push 打断/抵消仲裁保留空间。
- 让日志能明确报告本 tick 合并了哪些等价 deferred。

## Non-Goals

- 不判断 `Up` 和 `Down` 是否应该抵消。
- 不用 root wave 判断同一波传播是否已经访问过 subject。
- 不添加能量、TTL、强度、最大 fanout 等长期治理策略。
- 不改变 `ActionArbiter` 对 body intent、target claim、same-claim merge 的现有仲裁。

## Decisions

### Decision: 使用 queue-equivalence key 合并 deferred

合并 key 只描述“未来要执行的动作是否等价”，不描述“它由谁导致”。

建议 key 字段：

- `readyTick`
- `specId`
- `direction`
- resolved subject key

subject key 使用 `DeferredAction.SubjectEntityIds` 排序后拼接；如果为空则回退到 `EntityId`。`CreatedTick`、`CausalityId`、现有 `DedupeKey` 不参与等价判断。

合并不代表丢弃“有多少股同向推力汇入”。队列只保留一个等价 deferred action，但需要记录 contribution count，并保留有限 causality samples 供日志和未来 strength policy 使用。本次不计算强度、不改变移动结果，只把重复 action 数量从执行语义中剥离出来。

### Decision: 合并发生在回队列边界

合并点放在 `WorldActionQueue.EnqueueDeferred()` 内部。规则层仍然可以报告完整 `DeferredActions`，便于观察 raw propagation；队列层负责避免等价输出膨胀。

这样服务端 tick runner、sandbox/test helper，以及未来任何直接调用 `WorldActionQueue.EnqueueDeferred()` 的路径都会得到同一套合并语义。调用方需要知道结果时，`EnqueueDeferred()` 应返回能区分“新入队”和“已合并”的结果。

### Decision: 不合并不同方向

同一 subject 同一 tick 同时收到 `Up` 和 `Down` 是未来 push 打断、抵消或震荡策略的问题。本次不处理不同方向，避免把“防爆队列保护”升级成 gameplay 仲裁。

### Decision: 日志摘要优先于逐条刷屏

当 deferred 数量较多时，日志应输出总数、入队数、合并数、前若干条样本和重复 key 摘要，而不是完整展开每个 action。这样手动测试可以复制关键证据。

日志继续使用服务端现有日志入口，也就是 Hotfix 侧的 `Log.Info(...)`，由服务器已有 NLog 配置承接输出。样本上限先使用代码内固定常量，避免为调试保护引入新的 server config。

## Risks / Trade-offs

- 如果未来要表达“同 tick 多股同向推力叠加强度”，应基于合并后的 contribution count 计算 strength，而不是依赖重复 action 数量。本次只记录贡献数，不解释强度。
- 如果某些测试依赖 raw `DeferredActions.Count`，需要区分规则层 raw output 和 queue 层 consumed output。
- 在 `WorldActionQueue` 内合并会影响所有调用方，因此测试必须覆盖直接 enqueue 路径和 tick runner 路径。
