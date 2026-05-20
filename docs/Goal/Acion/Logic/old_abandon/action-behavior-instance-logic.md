# 行为层逻辑目标

## 结论

行为层只有一个核心主语：`ActionBehaviorInstance`。

每一次被系统处理的行为都必须形成一个行为实例。行为实例像 command 一样保存本次行为的身份、来源、目标、状态、预约、事实输出和裁决结果。

行为是否当前 tick 完成，不是行为类型差异，而是 `BehaviorRunner` 推进后的结果差异。

```text
ActionRequest
-> ActionBehaviorDefinition
-> ActionBehaviorInstance
-> BehaviorRunner.Step
-> BehaviorStepResult
```

`BehaviorStepResult` 描述本次推进后的生命周期结果：

```text
Completed
Running
Rejected
Failed
```

- `Completed`：本 tick 完成，输出结果后回收。
- `Running`：尚未完成，保存到运行中实例存储，后续 tick 或事件继续推进。
- `Rejected`：被规则拒绝，输出拒绝结果后回收。
- `Failed`：执行失败，输出失败结果并释放占用后回收。

同 tick 完成和跨 tick 完成不是两套行为语义。所有行为都过同一套实例和 Runner 模型。

## 设计原则

- 行为过程的主语是行为实例，不是实体，不是表现事件，不是某个特殊行为。
- 所有行为都创建行为实例。
- 所有行为实例都由 `BehaviorRunner` 推进。
- `BehaviorRunner` 可以一步完成，也可以内部使用状态图、时间轴或条件等待。
- 只有 `Running` 的实例进入运行中实例存储。
- 行为实例负责声明预约、输出权威事实、生成裁决输出。
- `CostTicks` 是行为推进参数，不能只对部分行为生效。
- 现有 `ActionSpec` 策略分支要升级为行为定义的一部分，而不是废掉重写。
- 表现层只能消费服务端投影后的事实，不能反推权威规则。

## 总流程

```text
WorldAction / DeferredAction
-> ActionRequest
-> ActionContext
-> Targeting
-> ActionBehaviorDefinition
-> ActionBehaviorInstance
-> BehaviorRunner.Step
-> BehaviorStepOutput
-> BehaviorStepResult
```

`BehaviorStepOutput` 包含：

```text
ActionResults
CommitProposals
DeferredActions
ReservationChanges
ActionFacts
Transitions
Reasons
```

当前 tick 完成：

```text
ActionBehaviorInstance
-> BehaviorRunner.Step
-> BehaviorStepResult.Completed
-> 应用 BehaviorStepOutput
-> 释放本实例持有的临时预约
-> 回收实例
```

继续运行：

```text
ActionBehaviorInstance
-> BehaviorRunner.Step
-> BehaviorStepResult.Running
-> 应用本 tick 已产生的 BehaviorStepOutput
-> 保存实例
-> 保存实例持有的预约索引
-> 后续 tick 或事件继续 Step
```

拒绝或失败：

```text
ActionBehaviorInstance
-> BehaviorRunner.Step
-> Rejected / Failed
-> 输出失败结果和必要事实
-> 释放预约
-> 回收实例
```

## ActionBehaviorDefinition

`ActionBehaviorDefinition` 是行为定义。

它不是另起一套配置，而是对现有 `ActionSpec`、targeting、blocked result、conflict、merge、interrupt、plan、commit 等配置的行为层收口。

当前已有配置已经能表达很多行为策略分支：

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

这些配置不应该被废掉。它们应该被纳入 `ActionBehaviorDefinition`，成为行为实例创建和 Runner 推进的输入。

行为定义负责描述：

```text
BehaviorId
RunnerId
Primitive
TargetingPolicy
SubjectPolicy
ReservationPolicy
FactPolicy
TimingPolicy
RuntimePayloadFactory
BlockedResultPolicy
ConflictPolicy
InterruptPolicy
MergePolicy
PlanPolicy
CommitPolicy
```

行为定义来自明确配置和注册模块。它不能通过 action 名字、entity 名字、tag 组合、表现层动画状态来隐式决定规则。

`primitive` 表达行为的基础意图。`RunnerId` 表达行为如何推进生命周期。两者不能长期混为一谈。

示例：

```text
primitive = Move
RunnerId = move_step

primitive = Move
RunnerId = move_timed

primitive = Move
RunnerId = move_charge

primitive = Spawn
RunnerId = spawn_step

primitive = Rotate
RunnerId = rotate_pivot
```

这样已有策略分支继续保留，同时行为实例推进入口变清楚。

普通 move、push、spawn、remove、rotate、door、charge、parry、carry 都应该有行为定义。

## ActionBehaviorInstance

`ActionBehaviorInstance` 是一次行为命令实例。

它至少包含：

```text
InstanceId
SourceActionId
OwnerActionId
SpecId
BehaviorId
RunnerId
Source
Subjects
CurrentState
StateData
ReservationScope
CreatedTick
UpdatedTick
RuntimePayload
```

字段语义：

- `InstanceId`：本次行为实例身份，用于审计、预约归属、事实归属、调试追踪。
- `SourceActionId`：触发本实例的原始 action。
- `OwnerActionId`：因果链 owner，用于 derived/deferred 行为追踪。
- `SpecId`：行为使用的配置。
- `BehaviorId`：行为定义标识。
- `RunnerId`：推进本实例的 Runner 标识。
- `Source`：行为来源。
- `Subjects`：本实例直接管理的实体集合。
- `CurrentState`：当前行为状态，可为空或单步态。
- `StateData`：通用状态数据索引或摘要。
- `ReservationScope`：本实例声明的权威占用。
- `RuntimePayload`：行为专属运行数据。

`ActionBehaviorInstance` 不能变成所有行为字段的大杂烩。行为专属数据必须放在独立 payload 中。

示例：

```text
MovePayload
PushPayload
RotatePayload
DoorPayload
ChargePayload
CarryPayload
```

## BehaviorRunner

`BehaviorRunner` 是行为实例推进器。

它回答的问题是：

```text
这一次行为实例这一步应该做什么？
```

它不强制每个行为都有复杂状态转换。普通 move 可以一步完成，rotate 可以内部使用多状态流程，door 可以等待条件，charge 可以按输入或 tick 推进。

`BehaviorRunner` 可以内部使用状态机，但状态机只是实现方式，不是行为层语义。

它也不是实体级状态机。实体级状态机回答的是：

```text
这个实体当前处于什么长期状态？
```

`BehaviorRunner` 至少支持：

```text
CreateInstance
Step
Exit
CanTransition
```

`BehaviorRunner` 必须能表达：

- 当前 tick 完成。
- 等待指定 tick。
- 等待条件满足。
- 进入 windup、contact、recover、release、complete、fail 等行为状态。
- 在状态进入、推进、退出时释放输出。
- 在状态变化时获得、保持、变更、释放预约。

`BehaviorRunner` 不能固定成某个特殊行为的三段式。不同的行为可以有不同推进方式。

## RunningBehaviorInstanceStore

`RunningBehaviorInstanceStore` 只保存 `BehaviorStepResult.Running` 的实例。

它不是行为分类概念，只是运行时容器。

它至少支持：

```text
Add(instance)
Get(instanceId)
Remove(instanceId)
StepDue(serverTick)
QueryBySubject(entityId, tick)
QueryByCell(coord, tick)
QueryByResource(resourceKey, tick)
```

不允许把已完成的同 tick 行为常驻保存到这里。已完成行为需要审计时，应写入 trace 或 fact log，而不是污染运行中实例存储。

## 预约语义

reservation 是行为实例的一等能力。

```text
SubjectLock
CellReservation
ResourceReservation
```

`SubjectLock` 表示行为期间哪些实体不能被其它行为直接操作。

`CellReservation` 表示行为期间哪些坐标被保留，不能被其它 commit 抢占。

`ResourceReservation` 表示非坐标资源占用，例如门状态、机关槽位、轨道、连接关系、机制 channel。

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

同 tick 完成的行为也可以声明 reservation。

```text
Completed
  reservation 参与本 tick 裁决
  行为完成后释放

Running
  reservation 进入运行中索引
  后续 tick 继续参与权威裁决
```

权威占用输入顺序：

```text
static blocking
dynamic occupancy
same tick behavior reservation
running subject lock
running cell reservation
running resource reservation
```

`ConflictPolicy` 必须显式。

当前可先实现：

```text
RejectIncoming
```

以下策略必须独立设计，不能隐式混入普通裁决：

```text
DeferIncomingUntilEnd
Replace
Cancel
Preempt
Queue
```

任何等待、排队、替换、抢占都必须重新进入 targeting 和 arbitration，不能复用旧世界事实。

## CostTicks

`CostTicks` 是行为推进参数。

它不属于某个特殊行为，也不能只在部分行为上生效。

```text
CostTicks == 0
  Runner 可以当前 tick 完成

CostTicks > 0
  Runner 可以进入 Running
  到目标 tick 后完成
```

普通 move 如果配置为 `CostTicks == 0`，Runner 当前 tick 完成。

普通 move 如果配置为 `CostTicks > 0`，Runner 返回 `Running`，实例进入运行中存储，持有必要预约，并在完成 tick 输出 commit 和 fact。

是否当前 tick 完成必须由行为定义和 Runner 决定，不能由旧执行管线绕过。

## ActionFact

`BehaviorRunner` 直接输出服务端权威事实 `ActionFact`。

`ActionFact` 表达世界里发生了什么，不表达客户端怎么播放。

示例：

```text
EntityMoved
EntityPushed
EntitySpawned
EntityRemoved
RotateStarted
RotateContacted
RotateCompleted
DoorOpened
ChargeReleased
```

分层关系：

```text
ActionBehaviorInstance
-> BehaviorRunner
-> ActionFact
-> Event Layer
-> PresentationFact
-> Client Presentation Translator
-> AnimationEvent / PlaybackPlan
```

`ActionFact` 和 `PresentationFact` 是投影关系。

```text
ActionFact
  服务端行为事实
  可参与服务端事件分发
  可包含完整裁决上下文
  不要求与网络字段一一对应
  不包含 Unity 动画策略

PresentationFact
  同步投影
  面向客户端表现层
  只保留客户端需要消费的字段
  不参与服务端规则裁决
```

同一个 `ActionFact` 可以投影成零个、一个或多个 `PresentationFact`。

客户端只能消费 `PresentationFact` 和 snapshot。

## 表现事实投影

表现事实投影层只负责把服务端 `ActionFact` 转成客户端可消费的同步事实。

允许职责：

- 投影显式 `ActionFact`。
- 分配稳定 fact id。
- 过滤不需要下发的服务端内部事实。
- 拆分或合并可播放事实。

禁止职责：

- 根据 action 名字推导特殊事实。
- 根据 transition reason 推导特殊事实。
- 根据 commit/result 猜复杂行为事实。
- 根据动画时长、曲线、本地表现状态反推权威事实。

复杂表现事实必须从 `BehaviorRunner` 输出的 `ActionFact` 投影而来。

例如：

```text
BodyMoved
RotateGroup
RotateImpact
DoorOpened
ChargeReleased
```

都必须来自明确的行为事实。

## Component / Effect 边界

component / effect 表达实体事实。

```text
PositionComponent
DirectionComponent
BlockingComponent
RotatePivotComponent
StunnedEffect
MovementDisabledEffect
```

behavior instance 表达一次行为过程。

```text
MoveActionInstance
PushActionInstance
RotateActionInstance
DoorOpenActionInstance
ChargeActionInstance
CarryActionInstance
```

component / effect 可以作为行为输入，也可以作为行为完成后的长期结果，但不能替代行为实例。

不要把一次多实体行为拆成多个 entity effect 再用 group id 拼回去。那会导致：

- 行为上下文碎片化。
- 事实输出归属不清楚。
- reservation 归属不清楚。
- 取消、失败、释放容易重复或遗漏。
- 调试和回放难以追踪。

实体状态机和行为 Runner 可以共存：

```text
EntityStateMachine
  表示实体长期状态

BehaviorRunner
  推进一次行为实例
```

## rotate 用例

rotate 只是统一行为实例的验证用例。

rotate success：

```text
ActionBehaviorInstance
  BehaviorId: rotate
  RunnerId: rotate_pivot
  Subjects: pivot + members
  Reservation: subjects + required cells

Start
  emit RotateStarted
  keep reservation

Complete
  commit final coords and directions
  emit RotateCompleted
  release reservation
  result Completed
```

rotate bounce：

```text
ActionBehaviorInstance
  BehaviorId: rotate
  RunnerId: rotate_pivot
  Subjects: pivot + members
  Reservation: original authoritative cells

Start
  emit RotateStarted
  keep original cells reserved

Contact
  emit RotateContacted
  emit downstream DeferredAction
  keep original cells reserved

Recover
  keep original cells reserved

Complete
  do not commit member coord changes
  release reservation
  result Completed
```

rotate bounce 的权威语义是：成员权威坐标没有离开原位。表现层可以播放探出和回弹，但服务端占用和预约一直以原坐标为准。

如果以后要做实体真实离开原坐标、途中可能被截断的行为，应定义新的行为语义，不能复用 rotate bounce。

## 需要改掉的现状

当前问题不是实现细节少，而是语义分裂。

需要消除：

```text
部分行为有实例，部分行为没有实例
部分行为过 Runner，部分行为不过 Runner
部分行为输出 ActionFact，部分行为靠表现投影层补事实
部分行为使用 CostTicks，部分行为配置了 CostTicks 但运行层忽略
部分预约跨 tick 生效，部分同 tick 占用没有统一实例归属
primitive 与生命周期推进方式混在一起
```

目标状态：

```text
所有行为都有实例
所有行为都过 Runner
所有行为都能输出 ActionFact
所有行为都按 Runner 解释 CostTicks
所有 reservation 都归属于行为实例
只有 Running 实例进入运行中存储
primitive 表达基础意图
RunnerId 表达生命周期推进方式
```

## 测试要求

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

Unity 自动测试必须覆盖：

- 普通 move 创建行为实例。
- 普通 move 由 Runner 当前 tick 完成。
- 普通 move 当前 tick 完成后不进入运行中实例存储。
- 普通 move 的 `ActionFact` 由 Runner 输出。
- `CostTicks > 0` 的普通 move 进入运行中实例存储。
- `CostTicks > 0` 的普通 move 在完成 tick commit。
- 同 tick reservation 参与当前 tick conflict。
- running reservation 参与后续 tick conflict。
- running subject lock 拒绝命中的 incoming action。
- running cell reservation 拒绝命中的 incoming commit。
- resource reservation 至少有查询和冲突测试。
- rotate success 由 Runner 输出 start 和 completion facts。
- rotate bounce contact 由 Runner 输出 contact fact 和 downstream action。
- rotate bounce 原权威坐标在完成前保持 reserved。
- 表现事实投影层不根据 action 名字或 transition reason 生成特殊事实。
- 显式 `ActionFact` 不会被投影层重复补同类事实。
- `primitive` 与 `RunnerId` 分离后，两个 move 行为可使用不同 Runner。

手动端到端验证：

- 服务端权威 Play Mode，两客户端观察同一 running behavior，最终权威状态一致。
- 普通 move 配 `CostTicks == 0` 时当前 tick 完成。
- 普通 move 配 `CostTicks > 0` 时进入 running，并在完成 tick commit。
- rotate success 最终坐标一致，客户端只播放服务端事实。
- rotate bounce 过程中尝试向原坐标生成或推动 block，服务端不允许抢占 reserved cell。
- rotate bounce contact 输出出现后，下游 push 重新进入普通 arbitration。

## 当前决策

- 行为层只有一个核心语义：`ActionBehaviorInstance`。
- 每个行为都有实例。
- 每个行为实例都过 `BehaviorRunner`。
- 当前 tick 完成只是 Runner 推进结果。
- 跨 tick 运行只是 Runner 推进结果。
- 只有 `Running` 实例进入运行中实例存储。
- `CostTicks` 由 Runner 解释。
- reservation 归属于行为实例。
- `ActionFact` 由 Runner 输出。
- 表现事实是服务端事实的同步投影。
- component / effect 表达实体事实，不替代行为过程。
- `ActionSpec` 现有策略分支应升级为 `ActionBehaviorDefinition` 的输入。
- `primitive` 表达基础意图，`RunnerId` 表达生命周期推进方式。
- rotate 是行为实例的一个用例，不定义行为层。
