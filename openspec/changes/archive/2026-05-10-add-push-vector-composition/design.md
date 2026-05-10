## Context

当前 push 传播链路是：

```text
WorldActionQueue
  -> StateDrivenRuleExecutionSystem
  -> ActionArbiter
  -> ResolvePushableBlock / TryResolvePushContacts
  -> DeferredAction
  -> WorldActionQueue.EnqueueDeferred
```

`dedupe-equivalent-deferred-push` 已经处理了同 tick、同方向、同 subject 的重复 deferred 入队膨胀，但它明确不处理不同方向仲裁。因此同一 subject 可以在同 tick 收到 `Up` 和 `Down`，并在相邻 tick 继续互相产出 deferred，形成震荡。

本次变更要处理的是 push 传播语义，而不是队列增长保护。仲裁应发生在 push deferred 输出进入普通 action 执行之前，或者在同一 tick ready actions 被路由成 move requests 前完成。它不能放进 `CommitResolver`，因为 commit resolver 只能处理已经形成的 move proposals，而上下震荡发生在 deferred push 产生 move proposal 之前。

## Goals

- 同一 ready tick、同一 resolved subject 的 push contribution 先合成为一个净向量。
- 相反方向贡献在向量层抵消，避免同 subject 同 tick 同时保留相反 push action。
- 合成后的移动仍然按格子离散步骤验证碰撞，不能跳过中间格。
- 保留 contribution 和 energy metadata，方便未来做能量、强度、衰减或质量系统。
- 保持当前 deferred 跨 tick 传播模型，不回到全链同 tick 求解。

## Non-Goals

- 不让合成向量直接移动多格。
- 不实现 diagonal 直线穿越、连续碰撞或物理速度。
- 不把 contribution count 解释成移动距离。
- 不改客户端同步或表现层。
- 不替代 existing body intent / target claim / commit validation。

## Decisions

### Decision: push vector composition 是 action 仲裁层的一部分

新增规则归属在 `ActionArbiter` 附近，建议抽为 `PushVectorArbiter` 或等价 helper，由普通 action pipeline 调用。它处理的是同 tick ready push actions 的语义合成，不是 `WorldActionQueue` 的去重。

输入是同 tick ready 的 push actions 或 deferred push actions。聚合 key 使用：

- `readyTick`
- resolved subject key

`subject key` 使用 resolved `SubjectEntityIds` 排序后的值；如果没有 subject ids，则回退到 action entity id。

第一版遵循“推就是推”：不同 action spec 只要产生 push contribution，就进入同一个默认合成池，不额外引入 action spec group 或 policy compatible 配置。未来如果出现确实不能混合的 push 类型，再单独扩展 composition group。

### Decision: 向量合成不等于飞行位移

每个 contribution 先映射成单位格方向向量：

```text
Up    => (0, 1)
Down  => (0, -1)
Left  => (-1, 0)
Right => (1, 0)
```

同一 subject 同 tick 的 contribution 逐项相加，得到 net vector。net vector 只决定“本 tick 净推动意图”，不直接代表可跳过路径的终点。

如果 net vector 是 `(0, 0)`，本 tick 不产生移动 action，原因记录为 push-vector-cancelled。

如果 net vector 是单轴非零向量，第一版只产生一个单位方向移动，贡献数和向量 magnitude 记录为 metadata，不推动多格。

如果 net vector 是双轴非零向量，第一版必须由确定性路径策略拆成多个单格步骤候选。默认顺序使用 X then Y：先执行水平单格，再执行垂直单格。每一步都要走现有 claim / plan / commit 碰撞验证；任一步被阻挡时，结果不得穿过阻挡格。

未来如果底层方向扩展为 8 向，diagonal 可以作为新的方向输入参与合成，但仍必须明确对应的碰撞路径或穿角规则，不能隐式绕过离散碰撞。

### Decision: 预留 energy metadata，但不解释能量

合成结果需要保存：

- total contribution count
- per-direction contribution count
- net vector
- optional energy value
- source causality samples

本次 `energy` 是结构占位。`contribution count` 表示同方向多股推力的增强事实，但第一版不把它解释成多格移动、更高优先级或额外 cost。未来如果要做强度、多格推、衰减或质量系统，应在这个 metadata 上扩展，而不是重新依赖重复 action 数量。

### Decision: 不改变现有 commit 防线

即使 push vector composition 产出一个移动意图，最终仍需要经过现有 body claim、target claim 和 commit validation。`CommitResolver` 的 `source changed`、`target reserved`、`blocked cell` 等验证仍然是最终安全边界。

## Risks / Trade-offs

- 双轴向量使用固定 X then Y 路径会带来路径顺序偏好；这是第一版为了确定性和可测试性接受的取舍。
- 如果 future energy 要影响距离，本次记录的 energy metadata 需要迁移到更正式的 strength policy。
- 如果只按 subject 合成，可能会隐藏“同一 body 不同 member 接收不同方向”的细节；因此 spec 要求保留 per-direction contribution 和 causality samples。
