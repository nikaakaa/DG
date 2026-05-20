# 行为层统一实例逻辑目标

> 本文档范围:**行为层 runtime 统一**。网络同步在 [network-sync-authoritative-tick-goal.md](../../network/network-sync-authoritative-tick-goal.md) 单独处理,本文不涉及网络协议、下行投影时序、客户端预测/纠错的具体规则,只在边界上声明"Runner 输出 ActionFact,投影由表现层另行处理"。

## 术语表与角色边界

行为层有三层运行时角色,不要混用。`ActionRuntime/` 是目录名/模块名,不是任何一层的角色。

```text
BehaviorRuntime(层:每 tick 编排器)
  每 tick 行为层唯一编排器。
  组合 BehaviorInstanceRunner + BehaviorRunnerRegistry + PolicyServices,
  负责:drain 意图 → 构造 candidate → 跑 admission → 调 BehaviorInstanceRunner 启动 Running 实例
        → 调 BehaviorInstanceRunner.StepDue 推进/释放 → 按依赖顺序 apply output → emit ActionFact。
  不持有 instance 状态机,不直接读写 world entity。

BehaviorInstanceRunner(层:运行中实例管理器)
  持有 RunningBehaviorInstanceStore 与 active claim 索引。
  对外接口:Start(instance) / StepDue(serverTick) / QueryByEntity / QueryByCell / QueryByResource / ReleaseByInstance。
  内部对每个到期或在 tick 的 instance 调 BehaviorRunner 推进。
  不构造 candidate,不跑 admission,不直接写 world,不解析 RunnerId。

BehaviorRunner(层:单实例推进器,即该 RunnerId 的状态机)
  对单个 ActionBehaviorInstance 调 IBehaviorRunner.Enter / Tick / Exit。
  输入:instance + 只读 world fact 视图 + spec + policy。
  输出:BehaviorStepOutput。
  每个 RunnerId 对应一个 IBehaviorRunner 实现(即该行为的状态机)。
  不直接访问 ActiveInstanceStore,不直接写世界。
```

三层调用方向只有自上而下:`BehaviorRuntime` 调 `BehaviorInstanceRunner`,`BehaviorInstanceRunner` 调 `BehaviorRunner`。反向调用一律不允许。

其他术语:

```text
Candidate
  尚未通过 admission 的 ActionBehaviorInstance。
  可以持有 trace 上下文,但不能持有 active claim,不能 emit 权威 fact。

Instance
  通过 admission 的 ActionBehaviorInstance。
  可以持有 active claim,Runner 推进它,可以 emit ActionFact。

ActionFact
  服务端行为层权威事实。Runner 输出,不可撤回。是行为层对外的唯一权威输出。

Claim
  对 subject / cell / resource 的占用声明,带 channel + mode。

ReservationScope
  代码符号别名,语义即 BehaviorClaimSet。新代码用 BehaviorClaimSet 指代。
```

## 行为即状态机(统一原则)

所有行为都是状态机,瞬时行为只是状态图退化为最小形态。这条原则适用于 move/spawn/remove 等单 tick 行为,也适用于 rotate/door/charge 等多 tick 行为。

```text
所有 RunnerId       → 必须实现 IBehaviorRunner
IBehaviorRunner   = 该行为的状态机
                    Enter / Tick / Exit 是状态机的推进入口
                    状态数据存于 instance.CurrentState + instance.StateData
```

状态图复杂度按行为而定,但接口、生命周期、扩展契约完全统一:

```text
move_step (状态图退化: Idle → Resolving → Completed)
  Enter @ tick N    读世界事实 + ActionSpec, 产出 CommitProposal.MoveEntity + ActionFact.EntityMoved
                    EndTick = N (或 N+1, 取决于实现)
  Tick @ tick N+1   返回 BehaviorStepOutput.Empty (状态图无需重读)
  Exit @ tick N+1   释放 claim, Completed

rotate_pivot_bounce (状态图复杂: Idle → Starting → Anticipating → Contacting → Recovering → Completing)
  Enter             emit RotateStarted, hold original cells, StateData.AnticipateUntilTick = N + k
  Tick @ k          重读世界事实, 决定 transition (Anticipating -> Contacting | Anticipating -> Completing)
  Tick @ k + m      Contacting -> Recovering -> Completing, emit RotateContacted / RotateCompleted
  Exit              释放 claim, Completed
```

`move_step` 和 `rotate_pivot_bounce` 走完全相同的 `IBehaviorRunner` 接口、相同的 `BehaviorInstanceRunner.Start / StepDue` 路径、相同的 lifecycle 五态(Running/Completed/Rejected/Failed/Cancelled)。不存在"瞬时行为绕过状态机"的简化路径。

每个 BehaviorId 的状态图草图必须随其 `IBehaviorRunner` 实现一同提交,即使只有一两个 node。状态图至少标:state 枚举、合法 transition、Enter/Tick/Exit 各自 emit 的 fact。提交位置:`Shared/DG.GameCore/ActionRuntime/Execution/Runners/<runnerId>/README.md`。

## 不变量

任何阶段都成立的硬约束。测试和 assert 围绕这些写。

```text
I1  每个 RunnerId 必须对应唯一 Runner 实现,实例运行中不可换。
I2  每个 ActionBehaviorInstance 同一时刻只有一个 state(Running / Completed / Rejected / Failed / Cancelled),不可并存。
I3  CostTicks >= 1,EndTick > StartTick,EndTick - StartTick >= EffectiveCostTicks。
I4  ClaimSet 在 admission 通过后不可被 Runner 自行 release 再 acquire,只能 extend 或在 Exit 一次性 release。
I5  ActionFact 一旦 emit 不可撤回,后续修正只能由 follow-up fact 表达。
I6  同 instance 不可跨 tick 改 RunnerId / BehaviorId / Subjects。中途状态变化只能 derive 新 instance。
I7  Candidate 不持有 active claim,不 emit ActionFact。
I8  ActiveInstanceStore 中所有条目状态必须为 Running。其他状态在本 tick output 应用后必须被移除。
I9  BehaviorRuntime 外层不直接读写 world entity 状态,所有写入经 CommitProposal 路径。
I10 行为层不产生 PresentationFact。表现层投影是 ActionFact 的下游,落在本文档之外。
I11 调用方向:`BehaviorRuntime` → `BehaviorInstanceRunner` → `BehaviorRunner`。反向调用不允许,跨层(Runtime 直接调 Runner)只允许在 Runtime 已通过 InstanceRunner 拿到 instance 引用之后,且不绕过 store 一致性。
```

## 单 tick 算法

`BehaviorRuntime.Tick(serverTick)` 行为层内部阶段。每阶段标注"由谁执行"(Runtime / InstanceRunner / Runner):

```text
stage 1  release-due                        [Runtime → InstanceRunner.StepDue → Runner.Exit]
  InstanceRunner 选出 store 中 EndTick <= serverTick 的 instance
  对每个 instance 调 BehaviorRunner.Step(Exit)
  收集 output,标记 instance 为 Completed
  InstanceRunner 从 store 移除,释放 claim

stage 2  drain-inputs                       [Runtime]
  从 InputQueue / DeferredQueue / AutoMove / PushOnEnter 拉本 tick ready 的 WorldAction
  按 priority + dedupeKey 排序,去重

stage 3  build-candidates                   [Runtime]
  对每个 WorldAction 解析 RunnerId / BehaviorId(读 ActionSpec + runtime fact)
  构造 candidate ActionBehaviorInstance(不入 store)

stage 4  admission                          [Runtime,读 InstanceRunner.store 快照]
  按排序逐个进入 admission:
    读 store 当前 claim 快照(经 InstanceRunner.Query*) + world fact + spec policy
    输出 Accepted(claim diff) / Rejected(reason) / Deferred(reschedule tick) / Replaced(target instanceId)
  Accepted:Runtime 调 InstanceRunner.Start(instance) 进入 Running 并 acquire claim
  Rejected / Deferred / Replaced 进入对应 output 收集

stage 5  tick-running                       [Runtime → InstanceRunner.StepDue → Runner.Enter/Tick]
  对 store 中所有 Running instance(包括本 tick 新进入的)由 InstanceRunner 调 BehaviorRunner.Step(Tick / Enter)
  BehaviorRunner 输出 BehaviorStepOutput,可以包含:
    CommitProposals / ActionResults / ActionFacts / DeferredActions / ClaimChanges(仅 extend) / Transitions / Trace

stage 6  apply-output                       [Runtime]
  按依赖顺序应用本 tick 所有 output:
    claim extension(经 InstanceRunner 改 store)
    -> commit proposals(按 priority 解析冲突)
    -> action results
    -> action facts(权威)
    -> deferred actions(写入 DeferredQueue,readyTick > serverTick)
    -> trace
    -> lifecycle state 写回

stage 7  hand-off(行为层结束)              [Runtime]
  把本 tick 的 ActionFact / ActionResult / EntityDelta hook 交给外层(表现层投影、网络同步、Snapshot、Housekeeping)。
  外层细节不在本文档范围。
```

确定性要求:

```text
stage 3 排序 key = (priority desc, sourceTick asc, dedupeKey asc, sourceActionId asc)
stage 4 admission 逐个,顺序与 stage 3 一致
stage 5 tick 顺序 = instanceId asc
stage 6 commit 冲突解析 = ConflictResolver 现有规则,优先级一致
```

## Reason taxonomy

`Rejected.Reason` 枚举(规则不允许):

```text
BlockedByTag
BlockedByCapability
BlockedByTargeting
BlockedBySubjectPolicy
BlockedByClaim
BlockedByConflictPolicy
BlockedByMergePolicy
BlockedByBlockedResult
BlockedByRuntimeEffect
BlockedByDeferredPolicy
BlockedByUnknownSpec
```

`Failed.Reason` 枚举(执行过程失败):

```text
CommitConflict
RuntimeEffectMissing
SubjectVanished
CellInvalidated
DependencyInstanceFailed
RunnerInternalError
```

`Cancelled.Reason` 枚举:

```text
ExplicitCancel
ReplacedByIncoming
PreemptedByPolicy
OwnerCancelled
SubjectRemoved
```

Reason 不允许复用 string。每个枚举值在 ActionFact 中作为稳定 id 携带。

## Cancel / Rollback 语义

```text
Cancel 传播
  Cancelled instance 的所有未派生 deferred 必须撤回(从 DeferredQueue 删除)。
  已派生但未 admission 的 candidate 必须丢弃。
  已通过 admission 的子 instance 是否一起 Cancel,由 ActionSpec.cancel_propagation 决定,默认 Propagate。

Failed 回滚
  Runner 在一个 tick 内 emit 的同 instance proposal 是一个事务边界。
  stage 6 内若该 instance 的某条 proposal 冲突被拒,本 tick 该 instance 的其他 proposal 不允许保留,instance 整体落 Failed。
  Runtime 对每个 instance 的 proposal 做事务边界,而非全局事务。

Cancel 与已 emit 的 ActionFact
  已 emit 的 ActionFact 不撤回(I5)。
  Runner 在 Cancel 时必须 emit BehaviorCancelled fact 作为补偿语义事实。
```

## Candidate / Admission 契约

Candidate 字段(admission 前):

```text
InstanceId        // 已分配,稳定
SourceActionId
OwnerActionId
SpecId
ResolvedBehaviorId
ResolvedRunnerId
Primitive
Source
ProposedSubjects
ProposedClaimSet
BaseCostTicks
TraceContext
ScheduledTick     // 期望 start tick
```

admission 输入:

```text
candidate
ActiveInstanceStore 快照(本 tick stage 1 之后的状态)
world fact 视图(只读)
ActionSpec
EffectSpec / Component / Tag 派生事实
policy:conflict / merge / interrupt / subject / plan / commit / cancel_propagation
```

admission 输出:

```text
Accepted(claim_diff, effective_cost_ticks, start_tick, end_tick)
Rejected(reason, fact, trace)
Deferred(next_ready_tick, reason)
Replaced(target_instance_id, reason)
```

admission 不直接修改 world。所有修改通过 stage 6 commit。

admission 必须是纯函数(对 store 快照 + world fact + spec 输入,产出确定输出),不依赖外部可变状态,以保证回放确定性。

## 行为目录 (Behavior Catalog)

当前阶段必须落地的行为。每行: BehaviorId → RunnerId → Primitive → 默认 channel → 默认 ClaimMode → 默认 CostTicks → 必发 fact。

```text
move_step              step_runner            move          movement      Exclusive   1   EntityMoved
move_blocked           step_runner            move          movement      Exclusive   1   BehaviorRejected
push_chain             push_runner            push          movement      Exclusive   1   EntityPushed
rotate_pivot_success   rotate_pivot_runner    rotate        movement      Exclusive   1   RotateStarted, RotateCompleted
rotate_pivot_bounce    rotate_pivot_runner    rotate        movement      Exclusive   1   RotateStarted, RotateContacted, RotateCompleted
spawn_entity           spawn_runner           spawn         interaction   Exclusive   1   EntitySpawned
remove_entity          remove_runner          remove        interaction   Exclusive   1   EntityRemoved
apply_runtime_effect   apply_effect_runner    effect        status        Shared      1   RuntimeEffectApplied
remove_runtime_effect  remove_effect_runner   effect        status        Shared      1   RuntimeEffectRemoved
debug_spawn            spawn_runner           debug         debug+其他    Exclusive   1   EntitySpawned
debug_remove           remove_runner          debug         debug+其他    Exclusive   1   EntityRemoved
debug_move             step_runner            debug         debug+其他    Exclusive   1   EntityMoved
debug_inspect          inspect_runner         debug         debug         Shared      1   (无)
deferred_replay        proxy_runner           varies        varies        varies      1   依宿主行为
```

debug 行为额外声明真实影响 entity/cell/resource,channel 写成 `debug + movement/status/interaction` 的组合 claim。

door / charge / carry / parry 不在当前阶段。新增时在本表追加一行,并新增 RunnerId。

每个 BehaviorId 的状态图与 `IBehaviorRunner` 实现见"行为即状态机"节。

## RuntimeEffect 与 BehaviorInstance 关系

```text
RuntimeEffect 是世界事实,生命周期跨 tick。
ActionBehaviorInstance 是一次行为命令,生命周期通常一到数 tick。

apply_runtime_effect
  一个 ActionBehaviorInstance,CostTicks == 1。
  Completed 时通过 CommitProposal.AddRuntimeEffect 让 RuntimeEffectInstance 进入 world。
  RuntimeEffectInstance 之后由世界自身维护,不再属于该 BehaviorInstance。

remove_runtime_effect
  一个 ActionBehaviorInstance,CostTicks == 1。
  Completed 时通过 CommitProposal.RemoveRuntimeEffect 让 effect 离场。

effect 过期
  由外层(housekeeping)触发,而非行为层 instance。
  过期事件 emit RuntimeEffectRemoved ActionFact,SourceActionId = effect 的 ownerActionId 或 system。
  本文档不规定 housekeeping 调度细节,但要求 housekeeping 调用入口走 BehaviorRuntime 产生 remove_runtime_effect candidate,而不是绕过行为层直接改世界。

effect 对 admission / claim 的影响
  当前 tick stage 4 读 effect 派生的 ComponentSourceContribution / TagContribution。
  effect 改变不重解析已运行 instance 的 RunnerId / BehaviorId(I6)。
  effect 可影响 admission 决策,不影响已通过 admission 的 instance 的 claim 边界。

stack policy
  按 EffectSpec.stack_policy 在 apply 阶段合并 / 替换 / 拒绝。
  替换路径必须 emit RuntimeEffectRemoved + RuntimeEffectApplied 两条 fact。
```

## Trace / 观测点(行为层内)

```text
TraceContext 字段
  TraceId(GUID,candidate 阶段分配,跟随 InstanceId 终身)
  ParentTraceId(派生时记录 owner)
  CreatedTick
  Origin(player input / auto / deferred / push_on_enter / debug / system)
  Tags(可选 string[])

Trace 事件最小集
  candidate_built
  admission_decided(accepted/rejected/deferred/replaced + reason)
  enter
  tick
  exit
  output_applied
  fact_emitted
  claim_acquired / claim_released
  cancelled / failed

Trace 输出
  默认 off,通过配置开关分级开启。
  开启时写入结构化日志 sink,不混入业务日志。

调试入口
  DGDebugPanel 显示 ActiveInstanceStore 当前条目 + 最近 N 个 closed instance 的 trace 摘要。
  MapChunkDebugWindow 显示 cell claim 持有者 instanceId。
```

## 验收口径 (Definition of Done)

行为层迁移完成的硬指标。任一未达不允许声明完工。

```text
代码侧
  grep -r "IActionStrategy" Shared/DG.GameCore/                                = 0
  grep -r "ActionStrategyRegistry" Shared/DG.GameCore/                         = 0
  grep -r "ActionUnitLifecycle\|ActionUnitStateMachine" Shared/DG.GameCore/    = 0
  grep -r "StateDrivenRuleExecutionSystem" Shared/DG.GameCore/ Server/         = 0
  所有 ActionSpec 解析出非空 RunnerId,启动期 fail-fast。

行为侧
  所有进入 BehaviorRuntime 的 WorldAction 都创建 candidate ActionBehaviorInstance。
  所有 Rejected / Failed / Cancelled 都归属某个 InstanceId 并 emit 对应 ActionFact。
  Runtime 外层无任何路径绕过 BehaviorRuntime 直接写世界。

测试侧
  AuthoritativeMoveVerification 三轮全绿。
  Unity EditMode 全套全绿。
  "测试矩阵"覆盖项全绿。

手测侧
  本文档"手动端到端验证"全部条目通过。
```

### 扩展性契约(硬性 DoD)

旧 `IActionStrategy` 唯一值得保留的是它的扩展性合同 —
新增一个行为不需要改主流程。这条契约必须迁移到 `Runner + BehaviorDefinition + ActionSpec`,并比旧版更严。

**主流程文件白名单(不允许新增行为时修改)**:

```text
Shared/DG.GameCore/ActionRuntime/Execution/StateDrivenRules.cs                                       # BehaviorRuntime 主循环
Shared/DG.GameCore/ActionRuntime/Execution/Runners/MoveBatchOrchestrator/MoveBatchOrchestrator.cs    # Arbitrated move/push 批编排
Shared/DG.GameCore/ActionRuntime/Execution/ActionBehaviorModels.cs                                   # Runner 抽象 / BehaviorInstance
Shared/DG.GameCore/ActionRuntime/Claims/ActionArbiter.cs                                             # 跨 Runner 全局仲裁
Shared/DG.GameCore/ActionRuntime/Claims/ActionClaims.cs                                              # Claim 数据模型
Shared/DG.GameCore/ActionRuntime/Networking/ActionRequestAdapter.cs                                  # WorldAction → ActionRequest
Shared/DG.GameCore/ActionRuntime/Facts/PresentationFactComposer.cs                                   # 投影层
```

**新增一种行为的合法操作集**:

```text
普通新行为(已有 primitive、已有 policy)
  新增 Runner 类(实现 IBehaviorRunner,即该 RunnerId 的状态机)
  新增 Runner README(含状态图)
  新增 BehaviorDefinition 配置项(behavior_id → runner_id)
  新增 ActionSpec 配置项(spec → behavior_id)
  新增 ActionPresentation 配置项
  在 BehaviorRunnerRegistry 注册 runner_id
  新增 EditMode 测试
  ✗ 不改主流程白名单

新 policy 能力(新 BlockedResultPolicy / 新 ConflictPolicy 等)
  新增 policy handler 类
  新增配置枚举/表项
  新增测试
  ✗ 不改已有 Runner 主体,除非该 Runner 显式消费这个 policy

新 primitive(全新行为形态,e.g. continuous-attack)
  新增 IBehaviorRunner 实现(自定义状态图)
  新增 commit / targeting / fact policy 必要扩展点
  新增配置
  新增测试
  ✗ 不改 BehaviorRuntime 主流程,只在扩展点接入
```

**验收测试(必须存在且常绿)**:

```text
DummyRunner_AddingNewBehaviorRequiresZeroMainFlowChanges
  - 新增 DummyRunner : IBehaviorRunner(Enter 输出固定 ActionFact 后 Completed 即可)
  - 在测试 setup 中注册 dummy BehaviorDefinition + dummy ActionSpec
  - 注册 dummy_runner 到 BehaviorRunnerRegistry
  - 提交一个 dummy WorldAction
  - 断言: tick 通过,产生 1 条 dummy ActionFact,无 transitions/exceptions
  - 断言: 该测试代码不 import / 不依赖主流程白名单文件
```

**违反信号**:

- 主流程白名单文件里出现 `if (spec.Primitive == X)` / `switch (BehaviorId)` / `Equals(new ActionStrategyId(...))` — 立即视为扩展性回归。
- 新行为 PR 修改了白名单文件 — 必须有 architectural justification,否则拒绝合入。

## 行为配置最终形态(开放扩展点契约)

`push / rotate / move / spawn / remove / apply_effect / remove_effect` 都是 `BehaviorDefinition` 行为单元。
它们之间的差异只来自 `runner_id` 与挂在 `BehaviorDefinition` 上的 policy slot 不同 ——
不是来自主流程里某个 `switch` 分支。

### 配置 vs 代码的边界

"配置化"不等于"无代码"。最终架构合同是:

```text
ActionSpec / BehaviorDefinition 负责引用扩展点
BehaviorRunnerRegistry / PolicyRegistry / TargetingRegistry 负责注册扩展实现
BehaviorRuntime 只解析、调度、应用输出
```

允许新增代码;但新增代码必须挂到 registry / policy / runner / selector 这类开放扩展点,
不允许借机改主流程白名单文件。

旧 `IActionStrategy` 体系最有价值的一点就是这条合同:**新增类 + 改配置**,不是"只改配置"。
迁移目标是把这条合同从 Strategy 搬到 Runner + BehaviorDefinition + 配置,并比旧版更严。

### 开放扩展点(允许新增实现并通过配置引用)

```text
Runner 实现 / 状态机    IBehaviorRunner / BehaviorRunnerRegistry
Batch behavior runner   IBatchBehaviorRunner / BatchBehaviorRunnerRegistry(legacy, 仅 move 批仲裁保留, 不作为新行为扩展点)
Targeting selector      ITargetSelector / TargetSelectorRegistry
Blocked result policy   IBlockedResultPolicy
Claim policy            channel + mode 组合 / IClaimPolicy
Fact / output projector ActionFact projector / IPresentationProjector
Condition evaluator     IPredicate / ICondition
Config row              Luban 表项 (ActionSpec / BehaviorDefinition / EffectSpec)
```

### 关闭路径(主流程禁止的写法)

```text
BehaviorRuntime 里 if (spec.Primitive == X) / switch (BehaviorId)             — 禁止
ActionArbiter 里 if (request.SpecId == ...) / 硬写某个行为名                   — 禁止
ConflictResolver 里硬写某个 ActionSpecId / BehaviorId                          — 禁止
StateDrivenRules / MoveBatchOrchestrator 里枚举某种 RunnerId                    — 禁止
表现层反推权威行为(用 PresentationFact 倒推 ActionFact)                       — 禁止
```

主流程只认识 `id`,不认识 push / rotate / spawn / remove 这种具体行为细节。
任何一处违反都视为扩展性回归,必须立即修复。

### push / rotate 作为行为单元的配置形态

`push_runner` / `rotate_pivot_runner` 仍然是代码,不可能纯表实现。
但主流程只通过 `BehaviorDefinition.runner_id` 间接引用它们,代码里不会出现 push / rotate 字符串。

```text
push_behavior
  behavior_id              = push_chain
  runner_id                = push_runner
  targeting_id             = direction_cell
  blocked_result_policy_id = push_or_block
  claim_policy_id          = movement_exclusive
  fact_policy_id           = entity_pushed

rotate_behavior
  behavior_id              = rotate_pivot_success
  runner_id                = rotate_pivot_runner
  targeting_id             = rotate_group
  blocked_result_policy_id = rotate_bounce_or_block
  claim_policy_id          = rotate_group_claim
  fact_policy_id           = rotate_timeline
```

新增一种 push 或 rotate 变体(e.g. `push_pierce` / `rotate_180`),合法操作集等同于"普通新行为":
新增 Runner / policy 实现 + 新增 BehaviorDefinition 配置 + 新增 ActionSpec 配置 + 新增测试。
**不允许**改 `BehaviorRuntime / MoveBatchOrchestrator / ActionArbiter / ConflictResolver`。

### 最终 DoD

```text
新增一种行为或行为变体时,只需要在开放扩展点新增实现,并通过配置引用它。
不允许修改主流程白名单文件,不允许在主流程加入针对该行为的判断分支。
```

## 测试矩阵

行 = BehaviorId,列 = lifecycle 结果。每个交叉点至少一条 EditMode 用例。

```text
                       Running  Completed  Rejected  Failed   Cancelled
move_step                 ✓         ✓          ✓        ✓          ✓
push_chain                ✓         ✓          ✓        ✓          ✓
rotate_pivot_success      ✓         ✓          ✓        ✓          ✓
rotate_pivot_bounce       ✓         ✓          ✓        ✓          ✓
spawn_entity              ✓         ✓          ✓        ✓          ✓
remove_entity             ✓         ✓          ✓        ✓          ✓
apply_runtime_effect      ✓         ✓          ✓        ✓          ✓
remove_runtime_effect     ✓         ✓          ✓        ✓          ✓
debug_spawn               ✓         ✓          ✓        ✓          ✓
debug_remove              ✓         ✓          ✓        ✓          ✓
debug_move                ✓         ✓          ✓        ✓          ✓
debug_inspect             ✓         ✓          ✓        n/a        ✓
deferred_replay           ✓         ✓          ✓        ✓          ✓
```

额外横向场景(每个至少一条用例):

```text
S1   同 channel exclusive claim 冲突 → 后入 Rejected
S2   不同 channel 或 Shared mode → 并行 Running
S3   runtime effect 改变 component → admission 拒绝某 spec
S4   runtime effect 改变 component → admission 解析出不同 RunnerId(尚未运行的 candidate)
S5   Cancel 传播 → 子 deferred 撤回
S6   Failed 回滚 → 同 instance 本 tick proposal 不落地
S7   admission 是纯函数 → 同输入两次调用输出完全一致
S8   存盘恢复后 Running instance 续跑到 Completed,结果与不存盘一致(行为层视角,不验证网络层)
```

## 结论

行为层只有一个运行时主语：`ActionBehaviorInstance`。

所有被系统处理的行为都必须创建行为实例。普通移动、推动、生成、移除、运行时效果、调试行为、rotate、door、charge、carry、parry、deferred action 都不能绕过实例和 Runner。

最终行为流程是：

```text
WorldAction / DeferredAction / InputIntent
-> ActionRequest
-> ActionBehaviorInstance candidate
-> BehaviorRuntime.Tick
-> BehaviorRunner
-> BehaviorStepOutput
-> BehaviorStepResult
```

Runner 不是外部规则管线里被临时调用的函数。Runner 是行为实例自己的运行时推进器。外部 tick 系统只负责创建候选实例、tick 行为运行时、应用 Runner 输出，不干涉 Runner 内部的 targeting、admission、claim、transition、commit 和 fact 输出。

不存在 `CostTicks < 1` 的行为。默认行为不会同 tick 创建又同 tick 完成。`CostTicks == 1` 表示 tick N 创建并进入运行，tick N + 1 完成、提交并回收。

## 生命周期结果

`BehaviorStepResult` 统一表达：

```text
Running
Completed
Rejected
Failed
Cancelled
```

- `Running`：行为实例仍在运行，保存实例状态和 active claim，后续 tick 继续推进。
- `Completed`：行为实例到达完成点，应用输出，释放 active claim，回收实例。
- `Rejected`：行为被 admission、claim 或规则裁决拒绝，不改变权威世界状态，输出拒绝结果、事实和 trace 后回收。
- `Failed`：Runner 执行过程失败，释放已获得 claim，输出失败结果、事实和 trace 后回收。
- `Cancelled`：行为被显式取消、替换、打断或抢占，释放 claim，输出取消事实后回收。

`Rejected` 和 `Failed` 不能混用。前者是规则不允许，后者是执行过程失败。

拒绝也必须属于行为实例生命周期。不能在外部因为 tag、effect、component 散落提前挡掉请求。

```text
WorldAction
-> ActionBehaviorInstance candidate
-> Runner admission
-> Rejected
-> output result / fact / trace
-> recycle
```

## CostTicks

`CostTicks` 是行为推进的基础 timing 输入，且最小值为 1。

```text
CostTicks == 1
  tick N 创建实例并进入 Running
  tick N + 1 完成、提交、释放、回收

CostTicks > 1
  tick N 创建实例并进入 Running
  tick N + CostTicks 或 Runner 计算出的完成 tick 完成
```

实际生命周期由 Runner 根据 timing policy、状态图、运行时 effect/component/tag、接触结果和等待条件计算。`CostTicks` 是基础输入，不是所有行为的固定真实长度。

rotate bounce、door、charge、wait 这类行为可以因为接触、恢复、蓄力、等待条件延长生命周期。客户端表现层只能消费 Runner 输出的事实时间线，不能用 `CostTicks` 反推权威生命周期或动画长度。

## Runner 和运行时事实

Runner 每 tick 读取当前世界事实，而不是只依赖创建时快照。

运行时事实包括：

```text
Component
WorldTag
RuntimeEffect
ActionSpec
Target state
World state
Active behavior instances
```

effect 通过 runtime effect 实例贡献 component 或 tag，component/tag/effect 是行为 admission、claim、transition 和 branch 的权威输入。

```text
EffectSpec
-> RuntimeEffectSpec
-> RuntimeEffectInstance
-> ComponentSourceContribution / TagContribution
-> final Component / WorldTag
-> Runner decision
```

RunnerId 和 BehaviorId 是实例创建/admission 后确定的运行时行为定义。它们可以由 `ActionSpec` 加当前 runtime facts 解析得到，但实例运行中不能偷偷更换 RunnerId 或 BehaviorId。中途状态变化只能触发 runner 内部 transition、cancel、fail、branch 或派生新的 behavior instance。

## 数据驱动边界

当前阶段不重构 Luban 配表总入口。

`ActionSpec` 继续作为主配置入口，`EffectSpec` 继续定义运行时 effect，component/tag/effect 继续作为数据驱动的运行时事实输入。之后可以再统一成 `BehaviorDefinition`、`BehaviorResolveRule`、`BehaviorClaimPolicy` 等表。

当前阶段必须吸收的已有配置包括：

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
EffectSpec.effect_payload_id
EffectSpec.duration_policy
EffectSpec.stack_policy
EffectSpec.tag
EffectSpec.can_move
EffectSpec.can_be_pushed
```

不允许通过 action 名字、entity 名字、表现状态、动画状态或 transition reason 隐式决定规则。可以通过显式的 ActionSpec、EffectSpec、component、tag、policy、注册表和 RunnerId 决定行为。

当前阶段 RunnerId 可以先由现有 `ActionSpec.primitive`、strategy id、runtime component/tag/effect 和代码注册表解析。后续再迁移到 Luban 表。

## ID 分层

这些 id 不能混用：

```text
ActionSpecId
  输入动作配置。表示玩家或系统发起了什么 intent。

EffectSpecId
  effect 配置。表示要添加哪种运行时 effect。

RuntimeEffectId
  某个实体身上的 effect 实例。表示来源、开始 tick、结束 tick、stack key 和因果。

ComponentKind / WorldTag
  当前世界事实。表示 entity 当前具备什么能力、状态或标记。

BehaviorId
  运行时行为语义。表示这次实例到底是什么行为。

RunnerId
  推进器实现。表示这次实例由哪个 Runner 推进。

InstanceId
  本次行为实例身份。用于 trace、fact、claim、调试和回放归属。
```

`ActionSpecId` 不等于 `BehaviorId`。`EffectSpecId` 不等于 `BehaviorId`。runtime effect 可以影响 BehaviorId/RunnerId 的解析，也可以只影响 admission、claim、timing 或 transition。

> 命名说明：当前代码中存在大量以 `Move*` / `move*` 为前缀的类型、方法、字段（例如 `MoveBatchOrchestrator`、`MoveActionStrategy`、`AuthoritativeMoveVerification`、`ClientMoveNetworkRuntime`、`AutoMoveComponent`），这些是行为层早期以「移动」为主用例孵化时遗留的历史命名，不代表行为层只服务于 move 或 movement channel。行为层运行时主语是 `ActionBehaviorInstance`，move 只是 primitive 之一、movement 只是 channel 之一。后续可在不破坏 wire/数据兼容的前提下将这些符号统一去 move 化，但本文档不依赖任何 `Move*` 命名定义行为层规则。`AutoMove` 是真实的 effect/component 概念，不属于命名遗留。

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
ClaimSet
CreatedTick
UpdatedTick
RuntimePayload
TraceContext
StartTick
EndTick
BaseCostTicks
EffectiveCostTicks
```

字段语义：

- `InstanceId`：本次行为实例身份。
- `SourceActionId`：触发本实例的原始 action。
- `OwnerActionId`：因果链 owner，用于 derived/deferred 行为追踪。
- `SpecId`：输入动作配置。
- `BehaviorId`：运行时行为语义。
- `RunnerId`：推进本实例的 Runner。
- `Primitive`：基础意图。
- `Subjects`：本实例直接管理的实体集合。
- `ClaimSet`：本实例声明的并行/互斥占用。
- `RuntimePayload`：行为专属运行数据。
- `TraceContext`：调试、回放、审计上下文。

一个 action 可以派生多个 behavior instance。deferred action 到期后必须重新进入 admission，创建新的 candidate instance，不能复用旧 instance，也不能跳过 targeting、claim 和 policy。

`ActionBehaviorInstance` 不应变成所有行为字段的大杂烩。行为专属数据放到独立 payload。

```text
ApplyEffectPayload
SpawnPayload
RemovePayload
RotatePayload
PushPayload
MovePayload
DoorPayload
ChargePayload
CarryPayload
ParryPayload
```

## Claim 和并行语义

行为并行不是由 entity 是否 active 写死决定，而是由显式 claim/channel 决定。

默认保守互斥。允许并行必须显式声明不同 channel 或 shared claim。

推荐最小 channel：

```text
main
movement
status
interaction
debug
presentation
```

推荐最小 claim：

```text
SubjectClaim(entityId, channel, mode)
CellClaim(coord, channel, mode)
ResourceClaim(resourceKey, channel, mode)
```

推荐最小 mode：

```text
Exclusive
Shared
```

示例：

```text
ApplyRuntimeEffect
  SubjectClaim(target, status, Exclusive 或 Shared)

DebugInspect
  SubjectClaim(target, debug, Shared)

DebugSpawn / DebugRemove / DebugMove
  debug channel 之外还必须声明真实影响的 entity/cell/resource

Move
  SubjectClaim(entity, movement, Exclusive)
  CellClaim(targetCell, movement, Exclusive)

Rotate
  SubjectClaim(pivot + members, movement, Exclusive)
  CellClaim(original / required cells, movement, Exclusive)
```

`ReservationScope` 作为代码符号别名长期存在,语义即 `BehaviorClaimSet`,承担 subject/cell/resource 索引职责,不再被限定成格子预约。

## Active Instance Store

运行中实例存储保存 `Running` 的行为实例和 active claim 索引。

至少支持：

```text
Add(instance)
Get(instanceId)
Remove(instanceId)
StepDue(serverTick)
QueryByEntity(entityId, channel, tick)
QueryByCell(coord, channel, tick)
QueryByResource(resourceKey, channel, tick)
ReleaseByInstance(instanceId)
```

当前阶段可以先兼容：

```text
byInstanceId
byEntityId
byCell
```

但接口设计要面向 channel，避免后续并行行为再拆一次。

## Conflict / Merge / Interrupt

incoming behavior 的裁决输入是：

```text
incoming ActionBehaviorInstance candidate
incoming ClaimSet
active behavior instances
active ClaimSet
ActionSpec conflict policy
ActionSpec merge policy
ActionSpec interrupt policy
runtime component/tag/effect
```

当前阶段只实现：

```text
allow
reject
已有 ready push vector merge
```

以下策略保持显式未支持，不能偷偷半实现：

```text
interrupt
replace
queue
preempt
cancel incoming / cancel active
defer incoming until active end
```

如果以后支持这些策略，必须重新进入 admission、claim 和 Runner 语义，不能复用旧世界事实。

## BehaviorStepOutput

Runner 输出统一为 `BehaviorStepOutput`。

至少包含：

```text
ActionResults
CommitProposals
DeferredActions
ClaimChanges
ActionFacts
Transitions
Reasons
Trace
```

输出应用顺序必须明确：

```text
claim / admission
-> commit proposals
-> action results
-> action facts
-> deferred actions
-> trace
-> lifecycle result
```

`PresentationFact` 不应作为 Runner 原生权威输出。客户端表现事实应由 `ActionFact` 投影而来。

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
BehaviorRejected
BehaviorCancelled
BehaviorFailed
RotateStarted
RotateContacted
RotateCompleted
DoorOpened
ChargeReleased
```

权威状态发生变化时必须有内部 `ActionFact` 或 trace。是否下发为 `PresentationFact` 由 projection/fact policy 决定。

表现事实投影层只负责：

- 投影显式 `ActionFact`。
- 分配稳定 fact id。
- 过滤不需要下发的内部事实。
- 拆分或合并客户端需要的可播放事实。
- 做受限 fallback。

禁止：

- 根据 action 名字推导特殊事实。
- 根据 transition reason 推导特殊事实。
- 根据 commit result 猜复杂行为事实。
- 根据动画时长、曲线、本地表现状态反推权威事实。

## Component / Effect 边界

component / effect 表达实体长期事实或临时事实来源。

```text
PositionComponent
DirectionComponent
BlockingComponent
RotatePivotComponent
MovementPermissionComponent
RuntimeEffect
WorldTag
```

behavior instance 表达一次行为过程。

component / effect 可以作为行为输入，也可以作为行为完成后的长期结果，但不能替代行为实例。

不要把一次多实体行为拆成多个 entity effect 再用 group id 拼回去。那会导致行为上下文、claim、fact、取消、失败、释放和回放归属全部碎片化。

## Rotate 用例

rotate 只是统一行为实例、Runner、claim 和 ActionFact 的验证用例，不定义行为层。

rotate success：

```text
ActionBehaviorInstance
  BehaviorId: rotate_pivot
  RunnerId: rotate_pivot_runner
  Subjects: pivot + members
  Claim: subjects + required cells

start
  emit RotateStarted
  hold claim

complete
  commit final coords and directions
  emit RotateCompleted
  release claim
  result Completed
```

rotate bounce：

```text
ActionBehaviorInstance
  BehaviorId: rotate_pivot
  RunnerId: rotate_pivot_runner
  Subjects: pivot + members
  Claim: original authoritative cells

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
  release claim
  result Completed
```

rotate bounce 的权威语义是成员权威坐标没有离开原位。表现层可以播放探出和回弹，但服务端占用以原坐标为准。

如果以后要做实体真实离开原坐标、途中可被截断的行为，应定义新的行为语义和 Runner 配置，不能复用 rotate bounce。

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

- 不存在 `CostTicks < 1` 的行为配置。
- 任意行为在 tick N 通过 admission 后创建 `ActionBehaviorInstance` 并进入 Running。
- 任意行为在完成 tick 输出 commit。
- 任意行为在完成 tick 输出 `ActionFact`。
- 被拒绝的行为也创建 candidate instance，并输出拒绝 result / fact / trace。
- 已持有 active claim 的行为拒绝同 channel 同 scope 的 incoming 行为。
- 不同 channel 的行为在显式 shared 时可以并行（用 status/debug 与 movement 同时执行作为验证用例）。
- runtime effect/component/tag 会影响 admission 或 behavior resolution。
- deferred action 到期后重新创建 instance。
- spawn/remove/apply effect/debug 等 primitive 都创建 instance。
- rotate success 由 Runner 输出 start 和 completion facts。
- rotate bounce contact 由 Runner 输出 contact fact 和 downstream action。
- rotate bounce 原权威坐标在完成前保持 active claim。
- 表现事实投影层不根据 action 名字或 transition reason 生成特殊事实。
- 显式 `ActionFact` 不会被投影层重复补同类事实。

## 手动端到端验证

用户手动验证：

- 服务端权威 Play Mode，两客户端观察同一 running behavior，最终权威状态一致。
- 默认行为花费一 tick，tick N 开始、tick N + 1 落地。
- 某一行为运行期间，同 channel 同 scope 的其他行为被拒绝或按显式 policy 处理。
- 显式 shared 的不同 channel 行为可以并行。
- runtime effect 导致行为被拒绝时，客户端和日志能看到 instance 归属。
- rotate success 最终坐标一致，客户端只播放服务端投影事实。
- rotate bounce 过程中尝试向原坐标生成或推动 block，服务端不允许抢占 active claim。
- rotate bounce contact 输出出现后，下游 push 重新进入 admission 和 Runner。
- debug spawn/remove/apply effect 也走 instance 和 Runner，不绕过行为层。

## 当前决策

- 行为层唯一运行时主语是 `ActionBehaviorInstance`。
- 所有行为都创建 candidate instance。
- 拒绝、失败、取消也归属 instance。
- Runner 是行为实例运行时推进器，由 tick 系统推进。
- 外部不干涉 Runner 内部 targeting、admission、claim、transition、commit、fact。
- 不存在 `CostTicks < 1`。
- 默认行为不是 same-tick immediate。
- entity 行为不是天然全互斥。
- 并行/互斥由显式 channel/claim/policy 决定。
- 默认保守互斥，允许并行必须显式声明。
- effect/component/tag 是运行时事实，会影响 admission、claim、分支和必要时的 BehaviorId/RunnerId。
- BehaviorId/RunnerId 是运行时解析结果，但实例运行中不变。
- 当前阶段不重构 Luban 总入口。
- `ActionSpec`、`EffectSpec`、component、tag 继续作为数据驱动输入。
- `ReservationScope` 是 `BehaviorClaimSet` 的代码符号别名。
- `ActionFact` 由 Runner 输出。
- 表现事实是服务端事实的同步投影。
- rotate 是验证用例，不定义行为层。
