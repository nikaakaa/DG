# Change: 重构原子行为单元层

## Why
`claim-driven-action-arbitration` 已经让 `ActionRequest + ActionSpec -> ActionClaim -> planner/commit` 成为主路径，`data-driven-runtime-actions` 已经把行为静态配置和运行时请求分开，`runtime-component-results` 已经把 Ability / RuntimeEffect 约束在 final Component 结果之前。

当前剩余问题在 pending push 生命周期：旧模型仍围绕 `PushPropagationState.CurrentFrontEntityId` 推进 front 链。它可以覆盖普通 pushable 的一部分表现，但不能表达“原始行为被阻挡后等待派生行为完成，再按自己的 cost tick 重新仲裁”。这会让普通 push、port-connected body push、mechanism push、auto move、bounce 和后续效果触发行为继续退回各自的专用状态机。

本变更把运行时行为收口为 action unit：每个行为单元内部用 claims 原子提交，行为单元之间通过 derived / waiting / retry 按 tick 自然涌现。第一版 push 只支持一层 derived blocker；如果 derived push 自己还需要继续推另一个 pushable，则 owner action 失败且本链不移动。

## What Changes
- 引入 action unit 作为 Rules 层的运行时行为单位，明确 `OwnerActionId`、`ActionUnitId`、`ParentUnitId`、`DerivedFromUnitId`、`createdTick`、`readyTick`、`costTicks`、`retryCostTicks` 和结果状态。
- 将 pending push 从 front 链推进升级为 parent action unit 等待 derived action unit 完成后的 retry 机制。
- 明确 `ActionClaim` 只表达单个 action unit 内部的原子提交边界；跨 unit 的 push / port push 通过依赖结果和 retry 涌现。
- 让普通 pushable push、port-connected body push、mechanism push、auto move 和后续可配置 move-like 行为共享同一套 action unit lifecycle。
- 保留 `RuntimeEffect -> ComponentStateResolver -> final Component -> Rules/System` 边界；行为层只读取 final Component / tag / world state，不查询 Ability / RuntimeEffect。
- 将旧 `WorldAction.CostTicks / ReadyTick` 和 pending `StepCostTicks / NextStepTick` 收口为 action unit ready tick 语义。

## Impact
- Affected specs:
  - `atomic-action-behavior-layer`
  - `claim-driven-action-arbitration`
  - `data-driven-runtime-actions`
  - `shared-gamecore-entity-rules`
- Related current specs:
  - `runtime-component-results`
  - `client-world-runner`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`
  - `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Commit/ConflictResolver.cs`
  - `Shared/DG.GameCore/Rules/Connectivity/PortConnectionSystem.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/DataDrivenRuntimeActionTests.cs`
  - `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj`

## Non-Goals
- 不实现完整 GAS。
- 不引入 AttributeSet、GameplayCue、客户端预测或回滚。
- 不把 push 链改成同一 tick 内完整求解的大事务。
- 不让 `ClientMapWorld`、Unity UI 或客户端本地状态裁决服务端权威行为。
- 不让 System / Rules 查询 Ability、RuntimeEffect 或 EffectKind。
- 不把 `PositionComponent`、`DirectionComponent`、tick counter 纳入 `ComponentStateResolver` 来源合成。
