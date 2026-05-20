# 行为层统一实例逻辑目标

## 结论

行为层只有一个运行时主语：`ActionBehaviorInstance`。

所有被系统处理的行为都必须创建 `ActionBehaviorInstance`。普通移动、推动、生成、移除、运行时效果、调试行为、rotate、door、charge、carry、parry、deferred action 都不能绕过实例和 Runner。

行为是否当前 tick 完成，不是行为类型差异，而是 Runner 推进结果差异。

```text
WorldAction / DeferredAction
-> ActionRequest
-> ActionBehaviorDefinition
-> ActionBehaviorInstance
-> BehaviorRunner.Step
-> BehaviorStepOutput
-> BehaviorStepResult
```

`Completed` 表示本 tick 创建、本 tick 推进、本 tick 应用输出、本 tick 回收。它不进入 running store。

`Running` 表示本 tick 后仍未完成。只有 Running instance 进入 `RunningBehaviorInstanceStore`，并继续持有 reservation。

immediate fast path 只能表示“不进入 running store”，不能表示“不创建 instance”。

## 生命周期结果

`BehaviorStepResult` 必须统一表达：

```text
Completed
Running
Rejected
Failed
Cancelled
```

- `Completed`：行为完成，应用输出，释放本实例临时占用，回收实例。
- `Running`：行为未完成，应用本 tick 输出，保存实例和 reservation。
- `Rejected`：规则裁决拒绝，世界权威状态不应被改变，输出拒绝结果后回收。
- `Failed`：Runner 执行失败，释放已获得占用，输出失败事实和结果后回收。
- `Cancelled`：行为被显式取消、替换或抢占，释放占用，输出取消事实后回收。

`Rejected` 和 `Failed` 不能混用。前者是规则不允许，后者是执行过程失败。

## 唯一推进入口

`BehaviorRunner.Step(instance, context)` 是行为推进的唯一入口。

现有裁决组件不废弃，但必须成为 Runner 内部服务：

```text
ActionBehaviorInstance
-> BehaviorRunner.Step
   -> Targeting
   -> SubjectPolicy
   -> Arbitration
   -> Plan
   -> Reservation
   -> CommitProposal
   -> ActionFact
-> BehaviorStepOutput
-> BehaviorStepResult
```

`ActionArbiter`、`RulePlanner`、`ConflictResolver`、`CommitResolver`、blocked result、merge、interrupt、plan、commit 等能力都应被 Runner 通过显式 policy 调用。

Runner 外部不能再存在直接执行行为的规则管线。

## 数据驱动边界

行为定义必须来自显式配置和注册模块。

不允许通过 action 名字、entity 名字、tag 组合、表现状态、动画状态或 transition reason 隐式决定规则。

`ActionBehaviorDefinition` 至少描述：

```text
BehaviorId
RunnerId
Primitive
TargetingPolicy
SubjectPolicy
ReservationPolicy
FactPolicy
TimingPolicy
BlockedResultPolicy
ConflictPolicy
InterruptPolicy
MergePolicy
PlanPolicy
CommitPolicy
RuntimePayloadFactory
```

`ActionSpec` 继续作为主配置入口。现有字段应纳入行为定义输入：

```text
ActionSpec.primitive
ActionSpec.targeting_id
ActionSpec.blocked_result_policy_id
ActionSpec.conflict_policy
ActionSpec.interrupt_policy
ActionSpec.merge_policy
ActionSpec.subject_policy
ActionSpec.plan_rule
ActionSpec.commit_rules
ActionSpec.default_cost_ticks
BlockedResultBranch.conditions
BlockedResultBranch.result_kind
BlockedResultBranch.result_spec_id
```

`WorldTag` 只能表达来源、能力、状态、免疫、阻挡等规则输入或过滤条件，不能拼出隐藏行为。

`primitive` 表达基础意图，`RunnerId` 表达生命周期推进方式。

```text
Primitive = Move
RunnerId = step_runner

Primitive = Move
RunnerId = timeline_runner

Primitive = Move
RunnerId = charge_runner

Primitive = Rotate
RunnerId = rotate_pivot_runner
```

相同 primitive 可以使用不同 Runner。Runner 可以引用专属算法或 service，但必须通过显式 `RunnerId`、`PolicyId` 或配置字段注册和选择。

## ActionBehaviorInstance

`ActionBehaviorInstance` 是一次行为命令实例。

至少包含：

```text
InstanceId
SourceActionId
OwnerActionId
SpecId
BehaviorId
RunnerId
Primitive
Source
Subjects
CurrentState
StateData
ReservationScope
CreatedTick
UpdatedTick
RuntimePayload
TraceContext
```

`ActionId` 和 `InstanceId` 不能混为一谈。

- `SourceActionId`：触发本实例的原始 action。
- `OwnerActionId`：因果链 owner，用于 derived/deferred 行为追踪。
- `InstanceId`：本次行为执行实例身份，用于 reservation、fact、trace 和调试归属。

一个 action 可以派生多个 deferred 或 derived behavior。deferred action 再执行时必须重新创建 instance，并重新进入 targeting、arbitration 和 Runner。

`ActionBehaviorInstance` 不应变成所有行为字段的大杂烩。行为专属数据必须放到独立 payload。

```text
MovePayload
PushPayload
RotatePayload
DoorPayload
ChargePayload
CarryPayload
```

## Runner 状态模型

Runner 必须支持数据驱动状态图或阶段图。

普通 move 也走状态图，但可以是最小图：

```text
enter -> resolve -> commit -> complete
```

rotate bounce 可以是：

```text
start -> contact -> recover -> complete
```

阶段配置至少支持：

```text
enter condition
duration or wait condition
reservation acquire / hold / release
commit proposal output
deferred action output
action fact output
action result output
transition condition
failure transition
cancel transition
```

Runner 可以内部使用状态机、时间线、条件等待或专属算法，但这些都是实现方式，不是绕过实例的理由。

## CostTicks 和实际生命周期

`CostTicks` 是行为推进的基础 timing 输入，不是固定生命周期长度。

```text
CostTicks == 0
  Runner 可以当前 tick Completed

CostTicks > 0
  Runner 可以 Running，并计算后续关键 tick
```

实际生命周期由 Runner 根据 `TimingPolicy`、状态图、接触结果、等待条件和运行时裁决结果计算。

Running instance 必须保存 Runner 计算出的关键时间点或 state schedule：

```text
StartTick
ContactTick
ReleaseTick
EndTick
StateSchedule
```

rotate success 可以按基础 `CostTicks` 完成。rotate bounce 可以因为 contact progress、recover policy 或额外 recovery ticks 延长完成时间。

door、charge、wait 类行为可以等待条件满足，不必被固定 tick 数限制。

客户端表现层只能消费 Runner 输出的事实时间线，不能用 `CostTicks` 反推权威生命周期或动画长度。

## Reservation 语义

reservation 是行为实例的一等能力。

```text
SubjectLock
CellReservation
ResourceReservation
```

reservation 至少包含：

```text
OwnerInstanceId
SubjectIds
CellCoords
ResourceKeys
AcquireTick
ReleaseTick
ConflictPolicy
```

只有 Runner 或 instance 可以声明 reservation。外部系统可以查询 reservation，但不能绕过 instance 自己挂占用。

同 tick Completed 行为也可以声明 reservation。它参与当前 tick 裁决，行为完成后释放。

```text
Completed
  reservation 进入本 tick 临时 reservation context
  参与本 tick conflict
  完成后释放

Running
  reservation 进入 running store 索引
  后续 tick 继续参与裁决
```

权威占用输入顺序固定为：

```text
static blocking
dynamic occupancy
same tick behavior reservation
running subject lock
running cell reservation
running resource reservation
```

当前先实现：

```text
RejectIncoming
```

以下策略必须独立设计，不允许隐式混入普通裁决：

```text
DeferIncomingUntilEnd
Replace
Cancel
Preempt
Queue
```

任何等待、排队、替换、抢占都必须重新进入 targeting、arbitration 和 Runner。

## RunningBehaviorInstanceStore

`RunningBehaviorInstanceStore` 只保存 `BehaviorStepResult.Running` 的实例。

它至少支持：

```text
Add(instance)
Get(instanceId)
Remove(instanceId)
StepDue(serverTick)
QueryBySubject(entityId, tick)
QueryByCell(coord, tick)
QueryByResource(resourceKey, tick)
ReleaseByInstance(instanceId)
```

同 tick Completed instance 不允许常驻保存到 running store。需要审计时写入 trace 或 fact log。

## BehaviorStepOutput

Runner 输出统一为 `BehaviorStepOutput`。

至少包含：

```text
ActionResults
CommitProposals
DeferredActions
ReservationChanges
ActionFacts
Transitions
Reasons
Trace
```

`PresentationFacts` 不应作为 Runner 原生权威输出。客户端表现事实应由 `ActionFact` 投影而来。

输出应用顺序必须明确：

```text
reservation / arbitration
-> commit proposals
-> action results
-> action facts
-> deferred actions
-> trace
-> lifecycle result
```

同 tick reservation 必须在同 tick conflict 中可见。

## ActionFact 和表现投影

`ActionFact` 是服务端权威事实，由 Runner 输出。

示例：

```text
EntityMoved
EntityPushed
EntitySpawned
EntityRemoved
RuntimeEffectApplied
RuntimeEffectRemoved
RotateStarted
RotateContacted
RotateCompleted
DoorOpened
ChargeReleased
BehaviorCancelled
BehaviorFailed
```

复杂权威事实必须来自 Runner，不能由表现投影层猜。

表现事实投影层只负责：

- 投影显式 `ActionFact`。
- 分配稳定 fact id。
- 过滤不需要下发的内部事实。
- 拆分或合并客户端需要的可播放事实。

禁止：

- 根据 action 名字推导特殊事实。
- 根据 transition reason 推导特殊事实。
- 根据 commit result 补复杂行为事实。
- 根据动画时长、曲线、本地表现状态反推权威事实。

如果需要默认事实，fallback 也应在 Runner output 阶段形成默认 `ActionFact`，而不是由 `PresentationFactComposer` 解释规则。

## Component / Effect 边界

component / effect 表达实体长期事实。

```text
PositionComponent
DirectionComponent
BlockingComponent
RotatePivotComponent
StunnedEffect
MovementDisabledEffect
```

behavior instance 表达一次行为过程。

component / effect 可以作为行为输入，也可以作为行为完成后的长期结果，但不能替代行为实例。

不要把一次多实体行为拆成多个 entity effect 再用 group id 拼回去。

## Rotate 用例

rotate 只是统一行为实例、Runner、reservation、ActionFact 的验证用例，不定义行为层。

rotate success：

```text
ActionBehaviorInstance
  Primitive: Rotate
  RunnerId: rotate_pivot_runner
  Subjects: pivot + members
  Reservation: subjects + required cells

start
  emit RotateStarted
  hold reservation

complete
  commit final coords and directions
  emit RotateCompleted
  release reservation
  result Completed
```

rotate bounce：

```text
ActionBehaviorInstance
  Primitive: Rotate
  RunnerId: rotate_pivot_runner
  Subjects: pivot + members
  Reservation: original authoritative cells

start
  emit RotateStarted
  hold original cells

contact
  emit RotateContacted
  emit downstream DeferredAction
  hold original cells

recover
  hold original cells

complete
  do not commit member coord changes
  release reservation
  result Completed
```

rotate bounce 的权威语义是成员权威坐标没有离开原位。表现层可以播放探出和回弹，但服务端占用和预约一直以原坐标为准。

如果以后要做实体真实离开原坐标、途中可被截断的行为，应定义新的行为语义和 Runner 配置，不能复用 rotate bounce。

## 需要改掉的现状

需要消除：

```text
active/timed 行为有 instance，ordinary/immediate 行为没有 instance
Runner 外部直接 arbitration / plan / commit
部分行为输出 ActionFact，部分行为靠表现层补事实
CostTicks 被旧 immediate 管线忽略
同 tick 占用没有统一 instance 归属
primitive 与生命周期推进方式混在一起
```

目标状态：

```text
所有行为都创建 ActionBehaviorInstance
所有行为都由 BehaviorRunner 推进
所有行为都能由 Runner 输出 ActionFact
所有 CostTicks 都作为 Runner timing 输入
所有 reservation 都归属行为实例
只有 Running instance 进入 running store
Completed instance 当前 tick 回收
primitive 表达基础意图
RunnerId 表达生命周期推进方式
表现层只消费投影事实
```

## 自动测试要求

只使用 Unity TestFramework 和现有 server verification。

OpenSpec 变更必须验证：

```text
openspec validate <change-id> --strict --no-interactive
```

Shared 行为层变更优先验证：

```text
dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore
```

服务端权威规则变更优先验证：

```text
dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore
```

Unity TestFramework EditMode 必须覆盖：

- 普通 move 创建 `ActionBehaviorInstance`。
- 普通 move 由 Runner 当前 tick `Completed`。
- 普通 move 当前 tick 完成后不进入 running store。
- 普通 move 的 `ActionFact` 由 Runner 输出。
- `CostTicks > 0` 的普通 move 进入 running store。
- `CostTicks > 0` 的普通 move 在完成 tick commit。
- rotate bounce 实际完成 tick 可以超过基础 `CostTicks`。
- 同 tick reservation 参与当前 tick conflict。
- running reservation 参与后续 tick conflict。
- running subject lock 拒绝命中的 incoming action。
- running cell reservation 拒绝命中的 incoming commit。
- resource reservation 至少有查询和冲突测试。
- deferred action 重新创建 instance，并重新进入 targeting 和 arbitration。
- spawn/remove/apply effect/debug action 创建 instance。
- rotate success 由 Runner 输出 start 和 completion facts。
- rotate bounce contact 由 Runner 输出 contact fact 和 downstream action。
- rotate bounce 原权威坐标在完成前保持 reserved。
- 表现事实投影层不根据 action 名字或 transition reason 生成特殊事实。
- 显式 `ActionFact` 不会被投影层重复补同类事实。
- `primitive` 与 `RunnerId` 分离后，两个 move 行为可使用不同 Runner 和生命周期。

## 手动端到端验证

用户手动验证：

- 服务端权威 Play Mode，两客户端观察同一 running behavior，最终权威状态一致。
- 普通 move 配 `CostTicks == 0` 时当前 tick 完成，但仍能在 trace/fact 中看到 instance 归属。
- 普通 move 配 `CostTicks > 0` 时进入 running，并在完成 tick commit。
- rotate success 最终坐标一致，客户端只播放服务端投影事实。
- rotate bounce 过程中尝试向原坐标生成或推动 block，服务端不允许抢占 reserved cell。
- rotate bounce contact 输出出现后，下游 push 重新进入普通 arbitration。
- debug spawn/remove/apply effect 也走 instance 和 Runner，不绕过行为层。

## 当前决策

- 行为层唯一运行时主语是 `ActionBehaviorInstance`。
- 所有行为都创建 instance。
- 所有行为都由 Runner 推进。
- Runner 是 arbitration、plan、reservation、commit、fact 输出的唯一入口。
- 行为定义必须数据驱动。
- `ActionSpec` 现有策略字段升级为 `ActionBehaviorDefinition` 输入。
- `CostTicks` 是基础 timing 输入，不是固定生命周期长度。
- 实际生命周期由 Runner 根据 timing policy、状态图和运行时结果计算。
- reservation 归属于 instance。
- 同 tick Completed instance 不进入 running store，但 reservation 参与本 tick conflict。
- `ActionFact` 由 Runner 输出。
- 表现事实是服务端事实的同步投影。
- component / effect 表达实体长期事实，不替代行为过程。
- `primitive` 表达基础意图，`RunnerId` 表达生命周期推进方式。
- rotate 是验证用例，不定义行为层。
