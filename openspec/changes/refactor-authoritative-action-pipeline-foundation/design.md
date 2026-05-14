## Context

DG 当前权威行为主链路已经大致是：

```text
WorldActionQueue
  -> ActionRequestAdapter
  -> ActionSpecRegistry
  -> ActionStrategyRegistry
  -> ActionArbiter
  -> RulePlanner
  -> CommitResolver / ConflictResolver
  -> GameWorld
  -> WorldDelta
```

源码现状里已经有可复用的基础：

- `ActionRequest` 有 `ActionSourceContext`、`EntityId`、`ActionTarget`、`OwnerActionId`、`DerivedFromUnitId`、`SubjectEntityIds`。
- `ActionTargetSelector` 目前只覆盖 `TargetCoordOneStep`、`TargetCoordAny`、`DirectionFromRequest`、`DirectionFromComponent`。
- `ActionArbiter` 现在仍然直接串起 target、subject、tag gate、claim build、blocked outcome 和 conflict。
- `ActionClaimBuilder` 只会从一个 resolved body 和 direction/target coord 生成 `BodyMove` claims。
- `RulePlanner` 已能从 accepted claims 创建 `MovePlan`，这是正确方向。
- `refactor-action-policy-pipeline` 已经把 strategy registry、显式注册和 no-old-pending 作为活动边界。
- `refactor-auto-move-self-push` 已经要求 self push 显式记录 source/target context，并禁止用 child feedback 或 pending parent retry 实现反向。

缺口是：当前还没有独立的 `ActionContext -> TargetData -> ExecutionOutput` 数据模型，所以新增多目标行为时只能继续扩大 `ActionTargetSelector`、`ActionArbiter` 或特殊 strategy。

## Goals

- 把运行时 action 上下文从 `ActionRequest.EntityId + ActionTarget` 升级为明确的 `ActionContext`。
- 把 target selection 从“算一个 direction/coord”升级为 `TargetData` 查询结果集合。
- 把 strategy 输出从“把 request 放进 moveRequests 或 proposals”升级为 `ExecutionOutput`。
- 让多个 target data 能稳定 fanout 成多个 claims。
- 让 all-or-nothing / partial success 成为 execution/arbitration/planning 可以消费的显式策略入口。
- 让 AutoMove self push 的 source/target/result owner 不靠 pending parent/child。
- 保持现有 claim、planning、commit 作为最终安全层。

## Non-Goals

- 不引入 Unity DOTS 到 Shared GameCore。
- 不把 `TargetData` 做成完整查询语言。
- 不让 `ExecutionOutput` 直接修改 `GameWorld`。
- 不把 partial success 变成默认行为；默认仍然 all-or-nothing，partial 必须显式配置。
- 不改变客户端只镜像服务端最终结果的边界。

## Decisions

### Decision: ActionContext 是运行时归属边界

`ActionContext` 表示一次 action 的权威运行时事实，不是世界组件，也不是策略本身。它至少需要表达：

- `ActionId`
- `OwnerActionId`
- `ActionSpecId`
- `InstigatorEntityId`
- `SourceEntityId`
- `CauserEntityId`
- `SubjectEntryEntityId`
- `TargetHint`
- `Direction`
- `CreatedTick`
- `ReadyTick`
- `CostTicks`
- `ClientTick`
- `InputId` 或等价 causality

`ActionContext` 只承载本次 action 跨阶段必须传递的事实。它不保存可重新查询的世界事实，不持有等待链状态，不决定 blocked/push/bounce 策略。

### Decision: TargetData 是只读候选目标集合

`TargetData` 是 targeting/query 阶段输出，不是 commit，也不是 claim。它可以表达：

- target entity
- target coord
- target body / subject key
- hit cell
- hit order
- distance
- direction
- source query id

第一阶段必须支持：

- `Self`
- `DirectionCell`
- `FrontCells` 或等价 front row 查询
- `EntitiesAtTargetCells`

这足够覆盖 AutoMove self push、普通方向移动，以及“面前所有对象移动”的最小闭环。更复杂的 box/circle/component/tag 查询以后沿相同模型增加。

### Decision: ExecutionOutput 是策略产物，不是仲裁结果

strategy 或 execution policy 消费 `ActionContext` 和 `TargetData`，输出 `ExecutionOutput`。它可以包含：

- claim candidates
- commit candidates
- blocked outcome candidates
- deferred output candidates
- result owner / result mapping
- success policy，例如 all-or-nothing 或 partial allowed

`ExecutionOutput` 不直接写 `GameWorld`，也不绕过 claim arbitration、planning、commit。它只是把“这个 action 想做什么”结构化地交给后续阶段。

### Decision: 多目标 fanout 先做确定性有限集合

本阶段只要求有限、有序、可测试的多目标集合。fanout 必须保持确定性排序，例如 query order、hit order、coord order、entity id tie-break。多目标输出不能依赖字典枚举顺序。

“面前所有对象移动”的最小语义：

```text
ActionContext(source, direction)
  -> TargetData(front cells / entities)
  -> ExecutionOutput(move each target by direction)
  -> ActionClaim[] per target/subject
  -> claim arbitration
  -> planning
  -> commit
```

### Decision: all-or-nothing 是默认，partial success 是显式策略入口

默认 action unit 对自己的 required claims 使用 all-or-nothing：任一 required claim 失败，本次 unit 不报告整体成功。

partial success 可以作为后续策略，但必须显式进入数据模型，例如：

- `ExecutionSuccessPolicy.AllOrNothing`
- `ExecutionSuccessPolicy.PartialAllowed`
- per-target result grouping
- result owner mapping

本 proposal 要求数据入口存在，并用测试证明默认 all-or-nothing 不被意外破坏。partial 行为可以先只验证 schema/model/guard，不要求完整玩法落地。

### Decision: AutoMove self push 不依赖 pending parent/child

AutoMove self push 的 context 明确为：

```text
Instigator = AutoMove entity
SourceEntity = AutoMove entity
SubjectEntry = AutoMove entity
Targeting = Self 或 DirectionCell policy
Direction = DirectionComponent.Direction
```

它的 blocked 反向、result owner、cadence update 都通过 same action context、execution output、blocked policy 和 commit rule 表达，不通过旧 pending parent/child、child feedback、parent retry。

### Decision: 与活动变更的关系

`refactor-action-policy-pipeline` 负责把中央执行器拆成 strategy registry、tag gate、subject/target、claim arbitration、planning、commit 等模块，并禁止旧 pending chain。

本变更负责定义这些模块之间的关键数据契约：`ActionContext`、`TargetData`、`ExecutionOutput`、多 target claim fanout、success policy entry。

`refactor-auto-move-self-push` 负责具体 AutoMove 语义。它应消费本变更的数据流，而不是自己创造一套特殊上下文。

## Migration Plan

1. Proposal 阶段固定 spec、design、tasks，验证 OpenSpec。
2. Apply 阶段先新增模型和测试，不改外部行为。
3. 把 `ActionRequest` adapter 扩展为 `ActionContext` 构造边界。
4. 把现有 direction/coord target selection 包装成 `TargetData` 输出。
5. 把 move strategy / claim builder 改为消费 `ExecutionOutput`。
6. 增加 Self、DirectionCell、FrontCells/Entities 的最小 TargetData policy。
7. 增加多 target fanout tests。
8. 把 AutoMove self push 接到新 context/target/output 流。
9. 保持 planning/commit 的最终安全检查，通过 Shared build、server verification、Unity EditMode 后再进入手动端到端。

## Risks / Trade-offs

- 风险：新模型太泛，变成空抽象。
  - Mitigation：第一阶段只支持 Self、DirectionCell、FrontCells/Entities 和 move claims。
- 风险：和 `refactor-action-policy-pipeline` 重复。
  - Mitigation：本变更只定义数据契约和多目标能力，pipeline 拆分类仍归前置变更。
- 风险：partial success 被误认为已完整实现。
  - Mitigation：tasks 和 spec 明确本阶段只要求入口、默认 all-or-nothing 和 guard，完整 partial 玩法后续 proposal。
- 风险：AutoMove self push 又回到 hidden parent feedback。
  - Mitigation：spec 明确 result owner 来自 context，跨 tick deferred output 是独立 action。
