## Context
当前已归档的行为管线已经形成三层基础：

```text
WorldAction / ActionRequest / ActionSpec
  -> ActionArbiter / ActionClaim
  -> RulePlanner / MovePlan
  -> ConflictResolver / Commit
```

但 pending push 仍保留旧 front 链模型：

```text
PushPropagationState
  -> CurrentFrontEntityId
  -> FromPendingPush(front)
  -> front move accepted
  -> MarkMoveAccepted
```

这个模型只记住“当前最前面的实体”，不记住“原始 action 还在等待”。因此 port-connected body 撞到 pushable 时，pushable 可以作为 front 被推动，但玩家和 linked body 的原始 move 不会在 blocker 行为完成后按自己的 cost tick retry。

目标语义是：

```text
每个 action unit 在自己的 ready tick 结算。
每个 action unit 内部 claims 原子提交。
action unit 之间通过 derived / waiting / retry 到后续 tick 形成涌现结果。
第一版 push 只支持一层 derived blocker；如果 derived push 自己还需要继续推另一个 pushable，则 owner action 失败且本链不移动。
```

## Goals
- 定义 action unit 作为 Rules 层运行时行为语言。
- 保留 `CostTicks / ReadyTick` 语义，让行为到达 ready tick 后才进入仲裁。
- 将 pending push 从 front 链升级为 parent action unit 等待、派生和 retry。
- 让普通 pushable 链和 port-connected body push 使用同一套 action unit 机制。
- 保持每个 action unit 内部 claims 原子提交，unit 之间不做同 tick 大事务。
- 行为层只读取 final Component / tag / world state，不读取 Ability / RuntimeEffect。
- 保持 Shared GameCore 纯 C#，不引入 Unity / Fantasy 依赖。

## Non-Goals
- 不做完整 GAS。
- 不做客户端预测、回滚或动画表现层。
- 不把所有行为一次性迁移到 Luban 正式配置；第一阶段可以继续使用 Shared registry。
- 不让 pending 保存整条未来确定结果；retry 时必须重新读取当前 world final state。
- 不把 `PositionComponent`、`DirectionComponent` 或 runtime tick counter 放进 ComponentStateResolver 来源合成。

## Decisions
- Decision: 运行时行为统一称为 action unit。
  - Rationale: `ActionRequest` 是外部/调度输入，不足以表达 waiting、derived、retry、timeout 和 parent result。
- Decision: 第一版新增运行时 action unit state，而不是继续把所有生命周期塞进 `ActionRequest`。
  - Rationale: `ActionRequest` 继续保持轻量输入；action unit state 保存生命周期和 pending 关系。
- Decision: 第一版显式区分 `OwnerActionId`、`ActionUnitId`、`ParentUnitId` 和 `DerivedFromUnitId`。
  - Rationale: `OwnerActionId` 面向外部请求和客户端结果；`ActionUnitId` 面向内部调度；parent / derived id 表达等待关系。
- Decision: `ActionSpec` 仍是静态行为定义，包含 primitive、blocked policy、claim policy、commit policy、默认 priority、默认 cost 或 cost 来源。
- Decision: 第一版 default cost 沿用现有 `WorldAction.CostTicks` 和 pending `StepCostTicks` 语义。
  - Rationale: 后续可由 `ActionSpec` 提供默认 cost，但不改变“到 ready tick 才仲裁”的规则。
- Decision: `ReadyTick` 是行为结算时间；未到 ready tick 的 action unit 不得进入仲裁。
- Decision: `ActionClaim` 只表达单个 action unit 内部原子性。
  - Rationale: 一个 unit 的 claims 全部可提交才成功，否则该 unit rejected、failed、waiting 或 derived。
- Decision: parent action unit 被 blocker 阻挡时，可以派生 blocker action unit，并进入 waiting。
- Decision: derived action unit 完成后，parent 不直接成功，而是变为 ready-to-retry。
  - Rationale: parent retry 必须重新读取当前 world、final Component、port-connected body 和 blocker 状态。
- Decision: 第一版固定 `parent.ReadyTick = currentTick + parent.RetryCostTicks`，`RetryCostTicks` 默认 1。
- Decision: derived failure 直接让 parent failed/rejected，并保留 parent blocker id 与 child failure reason。
- Decision: pending chain 安全第一阶段使用 `visited entity/action ids + max retry + max pending ticks`。
  - Rationale: 暂不引入复杂通用依赖图求解器。
- Decision: 第一版不允许 push-derived child 再创建另一个 push-derived child；遇到第二层 pushable blocker 时 derived action 失败。
- Decision: 普通 pushable 链通过多个 action unit 分 tick 涌现。
- Decision: port-connected body push 与普通 push 使用同一机制。
- Decision: `StateDrivenRules` 只调度 ready action units、arbitration、planning、commit 和 pending state transition。
- Decision: `ConflictResolver` 仍负责单 tick 已 accepted plans 的原子 commit，不负责生成 derived action。

## Target Model
```text
Static action data
  ActionSpec / registry / future config

Runtime action input
  WorldAction / ActionRequest

Runtime action unit
  ownerActionId / actionUnitId / parentUnitId / derivedFromUnitId
  specId / source / target / direction / priority
  createdTick / readyTick / costTicks / retryCostTicks
  status / result / failureReason / retryCount / timeoutTick

Arbitration
  final component + tags + world state
  -> claims
  -> accepted / rejected / pending-derived / commit proposals

Pending unit state
  parent action waits for derived action
  derived success wakes parent retry
  derived failure fails parent with context
  retry re-enters arbitration with current world state

Commit
  accepted unit claims -> MovePlan / CommitProposal
  all claims of that unit commit atomically
```

## Current Gaps
- `PushPropagationState` stores `RootEntityId`, `CurrentFrontEntityId`, `Direction`, `Chain`, but not the parent action unit.
- `ActionRequestAdapter.FromPendingPush` rebuilds a request from `CurrentFrontEntityId`, which turns pending state into push front instead of retry parent.
- `StateDrivenRules.ApplyProposalResultsToPendingStates` marks pending complete when derived move succeeds, without waking parent action unit.
- `ResolvePushableBlock` uses `pendingStates.AddPush(...)` directly, so blocked policy still creates push-specific pending state instead of generic derived action unit.
- Existing tests cover linked member hits pushable creates pending, but do not cover derived blocker action completes, then parent port-connected body action retries and succeeds.
- Current specs still mention `BehaviorIntent` and pending push continuation in older terms; this change updates those specs to action unit wording.

## Migration Plan
1. 收口 proposal / design / tasks，并补齐相关 spec deltas。
2. Apply 第一阶段新增 failing tests，覆盖 ordinary pushable split-tick retry、port-connected body retry、derived action fail 后 parent fail。
3. 新增 action unit lifecycle model，先适配现有 `WorldAction` / `ActionRequest` 输入字段。
4. 将 `PushPropagationState` 替换或包装为 generic pending action unit state。
5. 将 `ResolvePushableBlock` 改为产生 derived action unit，并让 parent action unit waiting。
6. 将 derived action success/fail 映射为 parent retry/fail。
7. 简化 `StateDrivenRules`，移除 front-specific pending branch。
8. 保留旧行为测试，并新增 port-connected body 与普通 pushable 对照测试。

## Risks / Trade-offs
- Risk: 不支持传递 push 会让多个连续 pushable blocker 直接阻断。
  - Mitigation: 这是当前业务语义；测试和手动验证必须确认第二层 pushable 不移动。
- Risk: pending 状态比 front 链复杂。
  - Mitigation: 第一阶段每个 unit 同时只等待一个 derived child，且 push-derived child 遇到第二层 pushable 直接失败。
- Risk: parent retry 时 world 已变化，结果可能不同。
  - Mitigation: 这是服务端权威涌现行为的正确结果；retry 必须重新仲裁。
- Risk: 旧 `PushPropagationState` 测试可能表达旧语义。
  - Mitigation: 先加新测试证明目标行为，再迁移旧断言。
- Risk: 行为层与 runtime effect / component result 边界混淆。
  - Mitigation: 本提案只消费 final Component / tag / world state，禁止 System / Rules 查询 Ability / RuntimeEffect。

## Verification Strategy
- OpenSpec: `openspec validate refactor-atomic-action-behavior-layer --strict --no-interactive`
- Shared build: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Server verification: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity EditMode:
  - action unit lifecycle tests
  - ordinary pushable split-tick retry tests
  - port-connected body blocked-by-pushable retry tests
  - derived fail / timeout / retry limit tests
- Manual Play Mode:
  - 启动服务端和两个 Unity 客户端。
  - 验证普通 pushable 链按 cost tick 逐步推进。
  - 验证 port-connected body 等待 pushable derived action 完成后再 retry 并整体移动。
  - 验证两个客户端最终 WorldDelta / 坐标一致。
  - 不执行 Unity Player build。

## Resolved Boundaries
- 第一版 action unit cost 来源沿用现有 `WorldAction.CostTicks` 和 pending `StepCostTicks`。
- `ActionSpec` 可后续作为默认 cost 数据源，但不是本提案 apply 的前置条件。
- derived blocker action 成功后，parent action 固定在后续 tick retry；第一版默认 `RetryCostTicks = 1`。
- parent action 等待 derived action 失败时，parent failure reason 必须同时保留 parent blocker id 和 child failure reason。
- 第一版不允许 push 多层等待链；例如 `player waits boxA` 后，如果 `boxA` 还会撞到 `boxB`，则 owner action 失败且本链不移动。
- 普通 pushable 和 port-connected body push 共享一层 derived action unit retry；第二层 pushable blocker 是阻断条件。
