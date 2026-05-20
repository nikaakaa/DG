# StepRunner (RunnerId = `step_runner`)

Owns the per-tick **walk / single-step** pipeline. Every `move_step`, `move_blocked` and `debug_move` BehaviorId resolves to this Runner.

## 状态枚举

```
Idle        -> 未启动，无运行实例
Planning    -> Enter：合成 push 向量、仲裁请求、规划 MovePlan
Resolving   -> Tick：CommitResolver / ConflictResolver 解决冲突
Settling    -> Exit：把结果写回 actionResults / emit ActionFact
Completed   -> 一帧结束（CostTicks=1）
Rejected    -> Enter 阶段被仲裁拒绝
Failed      -> Resolving 阶段计划失败
```

## 合法 transition

```
Idle      -> Planning      (BehaviorRuntime stage 5 admission)
Planning  -> Resolving     (arbitration accepted, MovePlan 生成)
Planning  -> Rejected      (push vector cancelled / arbitration denied)
Resolving -> Settling      (commit proposal succeeded)
Resolving -> Failed        (commit proposal denied)
Settling  -> Completed     (apply 完成)
任何状态  -> Cancelled     (上游 cancel 通过 channel claim 强制释放)
```

## Enter / Tick / Exit emit 的 fact

| Phase     | 必发 ActionFact                                          | 备注                                          |
|-----------|----------------------------------------------------------|-----------------------------------------------|
| Enter     | (none) 或 `BehaviorRejected`                             | 仅当 admission 即拒绝时同 tick emit `BehaviorRejected` |
| Tick      | (none)                                                   | 单 tick 行为通常不进入持续 Tick                 |
| Exit      | `EntityMoved` (成功) 或 `BehaviorFailed` (commit 失败)   | `move_step` 成功必发 `EntityMoved`              |

`debug_move` 与 `move_step` 共用 emit 集合，但额外携带 `debug` channel 标识。

## Internal policy services

`StepRunner` 持有以下旧管线类作为 **private readonly** 字段，外部不可见：

- `ActionArbiter` —— 仲裁 move 请求
- `RulePlanner` —— 把 AcceptedAction 翻译成 MovePlan
- `ConflictResolver` —— 检查 plan 与 running claim 的冲突
- `CommitResolver` —— 检查 CommitProposal 是否能落地
- `PushVectorArbiter` —— 合成同 tick 多向 push 向量

API surface 仅暴露 `ComposePush / Arbitrate / TryPlanMove / ResolveCommitProposals / ResolveMovePlans`。后续里程碑把这些 API 内化到 `Enter/Tick/Exit` 三段后，外部签名再收缩为标准 `BehaviorRunner.Step(instance, context)`。

## 默认 claim

- channel: `movement`
- mode: `Exclusive`
- CostTicks: `1`

## 与 RunnerRegistry 的关系

`step_runner` 是 RunnerId 常量（`StepRunner.RunnerId`），在 M1 阶段尚未注册进 `RunnerRegistry.Default`，因为 `BehaviorRunner` 期望 `IBehaviorStateMachine` 单实例形态。M2/M3 阶段会在 StepRunner 内部把 policy services 重排进 Enter/Tick/Exit 时再统一注册。
