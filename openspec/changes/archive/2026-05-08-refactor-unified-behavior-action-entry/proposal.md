# Change: 统一行为层 action unit 入口

## Why
当前 `refactor-atomic-action-behavior-layer` 把移动类行为收口到 `ActionSpec -> ActionRequest -> ActionArbiter -> RulePlanner -> ConflictResolver`，但没有真正统一整个行为层入口。底层仍然混合了 body 多对象原子提交、parent retry、pending push continuation 和 port-connected body 特例语义，导致“行为原子”边界不清。

用户期望的底层模型是：所有外部输入、系统触发、机关推动、运行时效果结果和 push handoff 都先进入统一 behavior action unit；一个 action unit 只裁决自己的源对象或当前实体抽象，遇到可传递目标时派生目标 action unit，并以自己的结果分支结束，而不是等待目标完成后 retry。

## What Changes
- **BREAKING** 重新定义行为层统一入口：所有运行时行为必须先进入 behavior action unit intake，不再只有 move-like 行为走统一入口。
- **BREAKING** 重新定义 action unit 原子边界：一个 action unit 只提交自己的源对象或当前 entity 抽象结果，不在同一 action unit 内提交一串被推动物体。
- **BREAKING** pushable 命中语义改为 handoff：源 action unit 不移动、不等待 retry，而是产生目标对象的独立 action unit，并以 handoff 结果结束。
- **BREAKING** 移除 parent retry 作为普通 push 成功路径；pending state 只保留调度、冲突保护和派生追踪，不代表源 action 等待回来补移动。
- **BREAKING** 将 `ActionClaim`、`MovePlan`、`ConflictResolver` 从 body-member 原子提交语义迁移到 single unit commit 语义。
- 暂不处理 port-connected body 的最终建模；本变更允许把连接体暂时视作一个 entity abstraction，但不继续把普通 push 语义绑定到 port body 多成员原子提交。
- 更新 OpenSpec、EditMode 测试和服务端验证，证明旧 parent retry / 多对象提交语义不再被当作行为层真相。

## Impact
- Affected specs:
  - `shared-gamecore-entity-rules`
  - `claim-driven-action-arbitration`
  - `data-driven-runtime-actions`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`
  - `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Commit/ConflictResolver.cs`
  - `Shared/DG.GameCore/Rules/Connectivity/PortConnectionSystem.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/DataDrivenRuntimeActionTests.cs`
  - `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`

## Current Gaps
- `ActionArbiter.TryBuildMoveClaims` 通过 `BodyResolver` 生成 body member claims，不是单 action unit commit。
- `ConflictResolver.Resolve` 对 `MovePlan.Members` 循环提交多个 entity 移动，不是单源对象提交。
- `PendingActionState.MarkUnitAccepted` 会让 parent `ReadyToRetry`，仍然保留“前面完成后源对象再补移动”的语义。
- `StateDrivenRules.ApplyProposalResultsToPendingStates` 只有 parent ownerCompleted 后才写 owner result，说明源 action 没有在 handoff 时结束。
- 当前测试名和断言仍混合 `RetriesParentAfterDerivedSuccess`、`PortConnectedBodyActionUnit_RetriesAtomicallyAfterDerivedSuccess`、`NestedPushableBlock_FailsWithoutMovingChain`，需要重写成统一行为入口和 handoff 语义。

## Non-Goals
- 不实现完整 GAS。
- 不做客户端预测、回滚或表现层动画规则。
- 不在本变更内完成 port-connected body 的最终玩法建模；连接体是否拆为多个 action unit 之后单独开 change。
- 不做 Unity Player build。
- 不把 `ClientMapWorld` 或 Unity UI 变成服务端规则裁决者。
