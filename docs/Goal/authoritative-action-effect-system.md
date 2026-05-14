# DG 服务端权威 Action / Effect 系统长期目标

## 结论

DG 需要的不是继续修补当前 `MoveActionStrategy + ActionArbiter + PushVectorArbiter` 小管线，而是一个服务端权威的 Action / Effect 系统。它可以参考 UE GAS 的分层思想，但不能照搬 UE 的 UObject、Actor、Replication 或 Prediction 模型。DG 的真相源仍然是 Shared `GameWorld`，Fantasy 负责服务端外壳和网络，Unity 客户端只做镜像和表现。

这个系统本身就是复杂的。复杂度不应该被藏在 `if specId == "xxx"`、tag 组合、组件回调、pending 子链、或者客户端表现层里。复杂度应该被拆成清楚的数据层、查询层、目标选择层、上下文层、裁决层、执行层、提交层和同步层。

目标不是第一版先做一个很差的简化版，而是把最终架构边界摆清楚，再按可验证切片实现。

## 为什么当前系统不够

当前系统已经有一些正确方向：

```text
ActionSpec
ActionRequest
ActionStrategy
ActionArbiter
ActionClaim
PushVectorArbiter
RulePlanner
CommitProposal
CommitResolver
WorldDelta
```

但它仍然偏窄，主要围绕“一个入口 entity 做一次 move / push”：

```text
一个 action
一个 entityId
一个 target coord 或 direction
一个 subject
一组 BodyMove claims
blocked 后 reject / bounce / deferred push
```

这能支持玩家移动、自动移动、机关推动、连接体推动的一部分语义，但不能干净支持更通用的能力，例如：

```text
让面前所有对象移动
选择扇形范围内所有可推动对象
对多个目标分别应用不同效果
根据目标集合计算优先级或冲突
同一个行为同时产生移动、方向、状态、伤害、运行时组件结果
一个技能拥有 instigator / causer / source object / target data / effect context
```

如果继续在当前结构上硬塞，会把复杂度挤进错误位置：

```text
ActionArbiter 继续膨胀
PushVectorArbiter 变成半个技能系统
BlockedResultPolicy 变成伪行为树
WorldAction 继续混合 source、target、subject、causality
CommitResolver 被迫识别玩法语义
客户端镜像开始复制服务端裁决
```

这些都不是长期架构。

## 总体分层

长期目标结构：

```text
Authoritative Tick
  Input / Trigger Intake
  Action Instance Creation
  Targeting / Query
  Condition Evaluation
  Policy Selection
  Execution Definition
  Claim Generation
  Arbitration
  Planning
  Effect Application
  Commit
  Journal / Delta / Sync
```

每层只做自己的事。

## 1. Trigger / Intake 层

Trigger 层负责把外部输入或世界事件转换成 action instance。

来源包括：

```text
玩家输入
AutoMove cost tick
PushOnEnter
运行时效果触发
AI 决策输出
调试工具
服务器脚本化事件
```

Trigger 层不裁决规则，不判断阻挡，不决定最终坐标。它只创建 action instance：

```text
ActionSpecId
Instigator
SourceObject / SourceEntity
Causer
InitialTargetHint
Direction
CreatedTick
ReadyTick
Cost
InputId / ClientTick / Causality
```

当前 `AuthoritativeWorldTickRunner.EnqueueAutoMoveActions` 就应该只属于 intake。它不应该知道 AutoMove 撞墙反向，也不应该知道 pushable 怎么处理。

## 2. Action Definition 层

Action Definition 对应 UE GAS 中 Ability / GameplayEffect 定义的一部分，但 DG 需要拆得更数据化。

Action 定义负责声明：

```text
ActionSpecId
Primitive / ExecutionKind
SourceKind
PriorityPolicy
CostPolicy
CooldownPolicy
TargetingPolicy
TargetFilterPolicy
ConditionPolicy
ClaimPolicy
ConflictPolicy
InterruptPolicy
MergePolicy
EffectList
BlockedPolicy
CommitPolicy
SyncPolicy
```

当前 `ActionSpec` 只覆盖了其中一小部分：

```text
primitive
source
priority
target_rule
blocked_result_policy
conflict / interrupt / merge
subject_policy
plan_rule
commit_rules
default_cost_ticks
```

长期要把它从“move 配置表”升级成“action 定义入口”，但不要把所有字段塞进一个巨大的表。合理拆分是：

```text
ActionSpec
TargetingSpec
ConditionSet
EffectSpec
ClaimSpec
PrioritySpec
CostSpec
BlockedPolicy
```

## 3. Action Context 层

Action Context 是一次行为实例的运行时上下文，不是组件，也不是长期世界状态。

它必须明确区分：

```text
Instigator：谁发起行为
SourceEntity：行为从哪个实体发出
SourceObject / Causer：由什么造成，可选
SubjectEntry：本次行为初始作用入口
TargetData：目标查询结果
Direction
CreatedTick / ReadyTick / Cost
OwnerActionId / Causality
ClientTick / InputId
```

当前 `WorldAction.EntityId` 同时承担了 source、subject entry、target entry 的多重含义，这是不够的。

例如 AutoMove self push 应该是：

```text
Instigator = AutoMove entity
SourceEntity = AutoMove entity
SubjectEntry = AutoMove entity
Direction = DirectionComponent.Direction
ActionSpec = auto_self_push
```

例如“面前所有对象移动”应该是：

```text
Instigator = 释放者
SourceEntity = 释放者或机关
Targeting = FrontAreaQuery
TargetData = 面前所有命中的 entity / coord / body
Execution = MoveTargets
```

上下文只保存本次 action 必须跨阶段传递的信息。当前 tick 可重新查询的世界事实不要塞进长期上下文里。

## 4. Targeting / Query 层

这是当前系统最缺的一层。

Targeting 负责从 context + world 生成 TargetData：

```text
Self
SingleEntity
TargetCoord
DirectionCell
FrontLine
FrontArea
BoxArea
CircleArea
ConnectedBody
EntitiesAtCells
EntitiesWithComponents
EntitiesMatchingTags
```

TargetData 不是最终 commit。它只是候选目标集合。

TargetData 应该能表达：

```text
目标 entity id
目标 coord
目标 body id
命中的 cell
命中顺序
距离
方向
来源查询 id
```

例如“面前所有对象移动”：

```text
TargetingPolicy = FrontArea
Range = 1
Width = all cells in front row or configured width
Filter = Has Position + Blocking/Pushable/Movable
Output = TargetData[]
```

当前的 `ActionTargetSelector` 只能处理：

```text
TargetCoordOneStep
TargetCoordAny
DirectionFromRequest
DirectionFromComponent
```

这只是 Targeting 的极小子集。

## 5. Condition 层

Condition 是底层谓词系统，负责读取 context、TargetData 和 `GameWorld` 最终事实，返回 true/false。

可以数据驱动的内容：

```text
条件类型
条件主体
组件要求
tag 要求
数值比较
目标数量
body 类型
距离
方向
是否可移动
是否可推动
是否阻挡
```

Condition 不允许：

```text
修改世界
产生 action
执行 commit
等待子节点
持有运行时状态
递归跑流程
```

它不是行为树。它是无状态 fact / predicate evaluator。

长期需要把当前散落的判断收拢：

```text
ActionTagGate
BlockedResultResolver
BodyCapabilityResolver
MovementPermissionComponent 判断
HasComponent / HasTag 判断
```

但不能把它做成可执行节点图。

## 6. Policy 层

Policy 负责用 condition 选择结果。

Policy 可以包括：

```text
PriorityPolicy
CostPolicy
TargetFilterPolicy
BlockedPolicy
ConflictPolicy
MergePolicy
InterruptPolicy
PartialSuccessPolicy
AllOrNothingPolicy
SyncPolicy
```

BlockedPolicy 示例：

```text
branch 10:
  conditions: Blocking CanBePushed
  result: EmitPush / DeriveAction / StructuredOutput

branch 20:
  conditions: Source Has Bouncable
  result: Bounce
  commit: SetDirectionOnBounce

branch 100:
  conditions: Always
  result: Reject
```

Policy 不应该知道 action name。普通行为差异来自 ActionSpec 选择哪个 policy。

## 7. Execution 层

Execution 是“这个 action 对 TargetData 生成什么结果意图”。

当前系统的 execution 被简化成：

```text
MoveActionStrategy -> moveRequests
SpawnActionStrategy -> CreateEntity proposal
RemoveActionStrategy -> DeleteEntity proposal
```

长期需要更明确的 execution types：

```text
MoveSelf
MoveTargets
PushTargets
ApplyEffectsToTargets
SetDirection
SpawnEntities
RemoveEntities
TeleportTargets
SwapPositions
EmitActions
```

例如“让面前所有对象移动”不是当前 MoveSelf，而是：

```text
Targeting: FrontArea
Execution: MoveTargets
ClaimPolicy: per target BodyMove claim
SuccessPolicy: AllOrNothing or PartialAllowed
```

Execution 生成的是中间结果：

```text
Claims
EffectApplications
CommitProposals
StructuredOutputs
```

它不应该直接写世界。

## 8. Claim 层

Claim 是权威裁决的关键抽象。它表达“这个 action 需要占用什么资源或改变什么状态”。

当前主要是：

```text
BodyMove claim
TargetCell claim
```

长期需要支持更多 claim 类型：

```text
MoveEntity
OccupyCell
ReserveCell
AffectEntity
AffectBody
ApplyEffect
ModifyDirection
ModifyComponent
SpawnAtCell
RemoveEntity
```

Claim 必须携带：

```text
ActionId
Instigator
Target
Subject
ClaimKind
From / To
Priority
Mode: Exclusive / Shared
GroupId
AllOrNothingGroup
```

“面前所有对象移动”会生成多个 target claims。它不能被压缩成一个 `EntityId + Direction`。

## 9. Priority / Conflict / Arbitration 层

优先级不是一个简单 enum 就完了。

长期优先级应该包含：

```text
Source priority
Action priority
Effect priority
Target priority
Tie-breaker
Owner / input order
Server tick
Deterministic sort key
```

Arbitration 负责：

```text
同一 entity 多个 action 谁赢
同一 target cell 多个 claim 谁赢
同一 component/effect 多个写入谁赢
是否可 merge
是否 interrupt
是否 partial success
是否 all-or-nothing fail
```

这层必须 deterministic。不能依赖字典遍历顺序、Unity 对象顺序、客户端到达顺序。

当前 `ActionArbiter.ResolveBodyConflicts` 和 `ResolveTargetClaims` 是雏形，但只覆盖 body move / target cell。

## 10. Planning 层

Planning 负责把 accepted claims 变成可提交计划。

它检查：

```text
source 是否仍在 from
target cell 是否仍合法
connected body member 是否一致
all-or-nothing group 是否完整
同一 entity 是否重复移动
空间索引是否允许
```

Planning 不应该重新决定：

```text
是否可推动
是否反弹
是否选择某个玩法分支
```

这些已经在 targeting / policy / arbitration 阶段决定。

## 11. Effect 层

Effect 是对目标产生的结果描述，不等于 commit。

Effect 可以是：

```text
MoveEffect
PushEffect
DirectionEffect
RuntimeComponentEffect
DamageEffect
DestroyEffect
SpawnEffect
TagEffect
StatusEffect
AnimationEffect
```

长期上，DG 的 `Effect` 层就是“小 GAS”里的 GameplayEffect 层。当前 `RuntimeEffectStore` / `RuntimeEffectSpec` / `ComponentStateResolver` 是这个方向的早期形态，但还没有被统一进 Action / TargetData / Execution / Commit 管线。

目标映射：

```text
GA / Action
  负责释放、目标选择、上下文、消耗、优先级、执行入口

GE / Effect
  负责对目标施加状态、组件结果、tag、数值、持续时间、堆叠规则

EffectApplication
  一次运行时效果实例，携带来源 action context、target data、开始 tick、结束 tick、stack key

RuntimeEffectStore
  存储仍在生效的 EffectApplication

ComponentStateResolver
  把静态组件源、运行时 effect、debug runtime source 合成为 final component result
```

EffectSpec 是静态定义：

```text
EffectKind
TargetBinding
Magnitude / Duration / StackPolicy
ApplicationPolicy
RemovalPolicy
ReplicationPolicy
```

EffectApplication 是运行时实例：

```text
ActionContext
TargetData
Resolved magnitude
StartTick / ExpireTick
Causality
```

当前项目已经有 `RuntimeEffectStore` 和 component result layer，但它还不是完整 action/effect 系统。长期应该让 action execution 产生 effect applications，再由 effect resolver 生成 final component result 或 commit proposal。

当前形态：

```text
RuntimeEffectSpec.AutoMove(target, interval, start, expire)
RuntimeEffectStore
ComponentStateResolver
final AutoMoveComponent
```

长期形态：

```text
EffectSpec: grant_auto_move
  TargetBinding = TargetData
  DurationPolicy = TimedTicks
  StackPolicy = Refresh / Replace / HighestPriority
  ComponentResult = AutoMove(interval)

ActionSpec: cast_auto_move_buff
  Targeting = SelectedEntity / FrontEntity / Area
  Execution = ApplyEffect
  Effect = grant_auto_move
```

执行流程：

```text
ActionContext + TargetData
  -> ExecutionOutput
  -> EffectApplication
  -> RuntimeEffectStore
  -> ComponentStateResolver
  -> final component / tag / stat result
  -> 后续 action arbitration 读取 final result
```

Effect 层不能直接裁决移动、推动、冲突和坐标提交。GE 可以改变后续规则看到的事实，例如：

```text
AutoMoveComponent
PushableComponent
MovementPermissionComponent
TagSetComponent
Stat modifier
Duration state
Stack state
```

真正的权威移动仍然必须走：

```text
Action
Targeting
Claim
Arbitration
Planning
Commit
```

所以最终关系是：

```text
GA 产生 Claims 或 GE Applications
GE Applications 改变 final component / tag / stat
final component / tag / stat 参与后续 GA 裁决
Commit 是唯一写世界坐标、方向、实体生命周期和 journal 的地方
```

这就是 DG 的“小 GAS”目标：不是照搬 UE GAS 的对象系统，而是保留 GA / GE / Attribute-or-Component Result / Tag / Effect Context / TargetData / Execution Calculation 这些核心分层。

## 12. Commit 层

Commit 是唯一写世界的位置。

CommitProposal 应该逐步扩展为更通用的写入语言：

```text
MoveEntity
SetDirection
CreateEntity
DeleteEntity
SetAutoMoveTick
AddRuntimeEffect
RemoveRuntimeEffect
SetComponentResult
AddTag
RemoveTag
RecordAnimationMetadata
```

CommitResolver 只做：

```text
验证提交条件
按 deterministic 顺序应用
写 GameWorld
写 DirtyJournal
写 WorldDelta 所需 metadata
返回 CommitResult
```

Commit 不做行为策略选择。

## 13. Journal / Delta / Sync 层

所有权威结果最终都通过 journal / delta 输出：

```text
ChangedEntities
RemovedEntities
AnimationMetadata
Direction changes
Runtime effect changes if needed
Diagnostics
```

客户端只消费结果：

```text
Apply snapshot
Play animation
Show feedback
Debug display
```

客户端不能做：

```text
本地裁决 push
本地决定 AutoMove 反向
本地决定冲突胜负
本地决定 effect 是否命中
```

## 14. AI / 行为树的位置

行为树不是禁用，而是不能放在权威规则裁决层。

可以有：

```text
AI BehaviorTree / UtilityAI / GOAP
  -> 选择 ActionSpecId
  -> 选择 target hint
  -> 提交 action request
```

不能有：

```text
BehaviorTree node 直接 MoveEntity
BehaviorTree node 直接 PushTarget
BehaviorTree node 直接 SetDirection
BehaviorTree node 直接等 child 成功再 retry
```

行为树解决“我想做什么”。Action / Effect 系统解决“这个想做的事情在权威世界里如何命中、冲突、裁决、提交、同步”。

## 15. ECS 优化关系

这个架构不阻碍 ECS，反而是 ECS 化前提。

ECS 友好的数据形态：

```text
ActionInstance[]
ActionContext[]
TargetData[]
ConditionResult[]
Claim[]
EffectApplication[]
CommitProposal[]
CommitResult[]
```

系统阶段：

```text
IntakeSystem
TargetingSystem
ConditionSystem
ExecutionSystem
ArbitrationSystem
PlanningSystem
EffectSystem
CommitSystem
DeltaSystem
```

不友好的形态：

```text
每个 entity 一棵运行中行为树
每个 action 一个复杂对象引用图
运行时节点持有等待状态
策略节点直接写世界
大量 action name if 分支
```

因此，长期文档必须先定义清楚数据边界，再考虑 `GameWorld` 内部 storage 是否换成自研 ECS 或 Arch adapter。

## 16. 示例：AutoMove self push

目标语义：

```text
AutoMove 到 cost tick
给自己释放一次 self push
push 成功则移动
push blocked 则自己反向
```

长期结构：

```text
Trigger:
  AutoMoveTickTrigger

ActionContext:
  Instigator = AutoMove entity
  SourceEntity = AutoMove entity
  SubjectEntry = AutoMove entity
  Direction = DirectionComponent.Direction

ActionSpec:
  Targeting = Self
  Execution = PushTargets or MoveSelfAsPush
  BlockedPolicy = AutoSelfPushBlocked
  CommitPolicy = SetAutoMoveTick

BlockedPolicy:
  CanMove -> success
  Blocked -> BounceSourceDirection

Commit:
  MoveEntity or SetDirection(SourceEntity, Opposite)
  SetAutoMoveTick(SourceEntity)
```

注意：这里不需要 child feedback，也不需要 pending parent retry。

## 17. 示例：面前所有对象移动

目标语义：

```text
释放者面前范围内所有符合条件的对象
按方向移动一格
冲突按优先级和 all-or-nothing policy 处理
```

长期结构：

```text
ActionSpec:
  Targeting = FrontArea
  TargetFilter = HasPosition + Movable
  Execution = MoveTargets
  ClaimPolicy = OneMoveClaimPerTarget
  SuccessPolicy = AllOrNothing or PartialAllowed
  ConflictPolicy = ExclusiveTargetCell

TargetingSystem:
  query front cells
  collect TargetData[]

ExecutionSystem:
  TargetData[] -> ActionClaim[]

ArbitrationSystem:
  resolve same target cell conflicts
  resolve same entity multiple writes

PlanningSystem:
  verify from/to state

CommitSystem:
  apply all accepted moves
```

当前系统做不了这个，因为它缺 TargetData、MoveTargets execution、multi-target claim policy、partial/all-or-nothing policy。

## 18. 分步落地建议

不要把这个长期系统一次塞进当前 `refactor-auto-move-self-push`。

建议拆分：

```text
1. refactor-action-context-ownership
   拆清 Instigator / Source / SubjectEntry / TargetDataHint

2. add-targeting-data-model
   引入 TargetData、TargetingPolicy、TargetFilterPolicy，但先只覆盖 Self / DirectionCell

3. refactor-action-execution-effects
   把 strategy 输出从 moveRequests/proposals 扩为 ExecutionOutput

4. add-multi-target-claims
   支持 TargetData[] -> Claim[]，覆盖“面前所有对象移动”

5. refactor-effect-application-layer
   统一 RuntimeEffect、component result、commit proposal 的关系

6. refactor-priority-conflict-policy
   扩展 priority、merge、interrupt、all-or-nothing、partial success

7. refactor-authoritative-sync-journal
   让 WorldDelta / animation metadata / diagnostics 统一吃 CommitResult / Journal
```

每一步都必须有：

```text
OpenSpec validate
Shared build
Server verification
Unity EditMode
用户手动 Play Mode / 双客户端验证
```

## 19. 停止条件

如果出现以下情况，说明又在错误地隐藏复杂度：

```text
ActionArbiter 新增 action name 分支
Condition 节点开始写世界
Policy 节点开始执行 action
CommitResolver 开始判断玩法名
Targeting 结果只塞回一个 EntityId
为了一个行为新增专用 server tick if
客户端开始复制服务端裁决
pending parent/child 被换名字恢复
```

出现这些情况要停下来，回到分层。

## 最终目标

DG 的长期目标是：

```text
服务端权威 Action / Effect 系统
数据驱动 ActionSpec / Targeting / Policy / Effect
确定性 Claim / Arbitration / Planning / Commit
GameWorld 作为唯一真相源
Unity 客户端只镜像和表现
Fantasy 只承载网络和服务端生命周期
未来 ECS 优化只替换数据存储和批处理，不改变规则语义
```

复杂度必须正面承认，放在正确层级里，而不是藏在当前 move/push 管线里。
