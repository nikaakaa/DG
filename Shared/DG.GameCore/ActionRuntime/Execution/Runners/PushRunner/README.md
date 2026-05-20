# PushRunner (RunnerId = `push_runner`)

集中持有 push 相关管线:
- 同 tick push 向量合成 (`PushVectorArbiter.Compose`)
- `push_on_enter` 触发 entity 进入 tile 时产出的 deferred move action 入队

`BehaviorRuntime` 不再直接 `new PushVectorArbiter()` 或在 stage 6 之外调用 `ExplicitOutputPolicies.EnqueuePushOnEnterActions`。

## 状态枚举

```
Idle             -> 当前 tick 无 push 输入
Composing        -> Enter：把同 subject 同方向 push 合成、抵消反向 push
ProducingDeferred-> Tick：扫 push_on_enter trigger，产生 deferred move action
Settling         -> Exit：把 deferred 动作回灌到 WorldActionQueue
Completed        -> 一帧结束（CostTicks=1）
Cancelled        -> 上游撤销（PushVectorCompositionResult.CancelledRequests）
```

## 合法 transition

```
Idle             -> Composing            (BehaviorRuntime stage 3 收集 push request)
Composing        -> ProducingDeferred    (合成后存在向量 / 触发 push_on_enter)
Composing        -> Cancelled            (反向 push 全部抵消)
ProducingDeferred-> Settling             (deferred move 入队完成)
Settling         -> Completed            (本 tick 结束)
任何状态         -> Cancelled             (上游 channel claim 强释放)
```

## Enter / Tick / Exit emit 的 fact

| Phase     | 必发 ActionFact                            | 备注                                              |
|-----------|--------------------------------------------|---------------------------------------------------|
| Enter     | `BehaviorRejected` (仅当反向 push 抵消)     | 抵消时 emit 给被取消的 subject                    |
| Tick      | (none)                                     | deferred 产出不立即 emit fact，等 StepRunner 完成 |
| Exit      | `EntityPushed`                             | 由触发后续 `move_step` 落地时间接 emit             |

`push_chain` 与 `push_on_enter` 共用 emit 集合，区分仅在于 `Source.OwnerActionId` 是否指向触发 tile。

## Internal policy services

`PushRunner` 持有以下 **private readonly** 字段:

- `PushVectorArbiter` —— 同 tick 多向 push 合成与方向抵消
- (静态委托) `ExplicitOutputPolicies.EnqueuePushOnEnterActions` —— `push_on_enter` deferred 入队 (M6 后内化)

API surface:
- `ComposePush(world, requests)` —— 同 tick push 合成
- `EnqueuePushOnEnterActions(world, queue, tick[, hasRunningMovementClaim])` —— deferred move 入队

## 默认 claim

- channel: `movement`
- mode: `Exclusive`
- CostTicks: `1` (push_chain) / `1` (push_on_enter，但带 release delay = `OutputCostTicks`)

## 与 StepRunner 的关系

`PushRunner.ComposePush` 在 `StepRunner.Arbitrate` 之前调用; 合成后的 `ActionRequest` 列表才进入仲裁。`push_chain` 和 `move_step` 共享 `movement` channel,所以 Exclusive claim 由 `BehaviorInstanceRunner` 统一管理。

## 与 RunnerRegistry 的关系

`push_runner` 是 RunnerId 常量 (`PushRunner.RunnerId`)。M2 阶段尚未注册进 `RunnerRegistry.Default`,因为 `BehaviorRunner` 期望 `IBehaviorStateMachine` 单实例形态;M6 阶段把 push 内部 policy 重排进 Enter/Tick/Exit 三段后统一注册。
