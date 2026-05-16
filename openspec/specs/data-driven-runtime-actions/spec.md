# data-driven-runtime-actions Specification

## Purpose
TBD - created by archiving change refactor-data-driven-runtime-actions. Update Purpose after archive.
## Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置、运行时行为输入和运行时行为单元分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划、提交策略、handoff policy、handoff subject policy 和默认 cost 来源；`ActionRequest` SHALL 只携带一次运行时输入的 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params；action unit state SHALL 保存 ready tick、lifecycle、parent / derived 关系、pending 结果和 push contact batch 归属。运行时行为输入 MUST NOT 复制 `ActionSpec` 的静态策略，也 MUST NOT 承担 pending 生命周期状态或 push contact batch 状态。

#### Scenario: 运行时请求引用配置
- **WHEN** player move, auto move, mechanism push, debug move, debug spawn, or debug remove is created
- **THEN** the runtime input references an `ActionSpec`
- **AND** the runtime input carries only request-specific source, target, direction, tick, client tick, source state, and runtime params
- **AND** action unit state carries ready tick, lifecycle, parent / derived ids, pending status, and push contact batch id when applicable
- **AND** runtime input does not copy `ActionSpec` claim, conflict, interrupt, merge, plan, commit, handoff, or default cost policy

#### Scenario: 修改普通行为策略
- **WHEN** a move-like behavior changes required component, blocked component, priority, merge policy, interrupt policy, blocked policy, or default cost
- **THEN** the change is made in the `ActionSpec` source or registry
- **AND** core execution does not add a new branch for the behavior name

### Requirement: 行为原语和行为来源分离
系统 SHALL 用行为原语表达执行类型，用 source context 表达行为来源。`Move`、`Spawn`、`Remove`、`SetComponentResult`、`ApplyRuntimeEffect` and compatible handoff primitives SHALL be primitives or primitive candidates; `Player`、`Auto`、`Mechanism`、`RuntimeResult`、`Debug` and `Handoff` SHALL be source / policy context, not separate execution branches for the same primitive. Runtime component/tag result primitives MUST produce commit proposals that add or remove source contributions and MUST NOT directly mutate final Component/tag state.

#### Scenario: 现有移动来源归一化
- **WHEN** 迁移现有玩家移动、自动移动、机关推动、调试移动和 push handoff
- **THEN** 它们都映射为 `Move` primitive or a move-compatible handoff primitive
- **AND** 它们通过 source context、priority、condition、claim、plan、commit 和 result branch policy 保留差异

#### Scenario: 新增普通移动行为
- **WHEN** 新增风场推动、陷阱拉动或冰面滑行这类普通移动行为
- **THEN** 行为配置复用 existing primitive and policy data
- **AND** 新增行为不需要新增 `WorldActionKind` 或 `BehaviorIntentKind` 业务分类 unless it introduces a new primitive

#### Scenario: 运行时结果进入同一层级
- **WHEN** runtime component result needs to cause movement, spawn, remove, or state mutation
- **THEN** it creates a behavior action unit instead of directly mutating authoritative world state
- **AND** the rule pipeline applies the result through the same arbitration and commit boundaries

#### Scenario: SetComponentResult writes source contribution
- **WHEN** a strategy emits `SetComponentResult`
- **THEN** commit records a component source contribution
- **AND** final component state changes only after resolver settlement

#### Scenario: Tag result writes source contribution
- **WHEN** a strategy emits `AddTag` or `RemoveTag` as a runtime result
- **THEN** commit records or removes a tag source contribution
- **AND** it does not directly edit final `TagSetComponent` as the runtime result path

### Requirement: 数据驱动仲裁 claim
系统 SHALL 在仲裁层从 ready action unit、`ActionRequest` 输入和 `ActionSpec` 生成 `ActionClaim`。仲裁 MUST use claims to decide body conflict, target occupancy, same-claim merge, exclusive resources, and derived action proposals instead of hardcoding per behavior-name conflict branches.

#### Scenario: 目标格独占
- **WHEN** two ready action units claim the same exclusive target cell
- **THEN** arbitration / conflict policy decides which unit may proceed
- **AND** the decision uses claim data instead of behavior-name branches

#### Scenario: 同 body 合并
- **WHEN** two ready action units from the same connected body request the same body move claim
- **THEN** merge policy may collapse them into a single accepted unit result
- **AND** no duplicate body movement is committed

#### Scenario: 同 body 冲突
- **WHEN** two ready action units from the same connected body request incompatible move claims
- **THEN** conflict policy rejects, interrupts, or fails the lower priority unit
- **AND** no partial body movement is committed

#### Scenario: 阻挡派生行为
- **WHEN** a ready action unit is blocked by a pushable blocker
- **THEN** arbitration may emit a derived action unit proposal
- **AND** the parent action unit enters waiting state instead of reporting success

### Requirement: 数据驱动冲突和打断策略
系统 SHALL 通过配置化 `ConflictPolicy`、`InterruptPolicy`、`MergePolicy` 和 priority 决定冲突、打断和合并。新增普通行为 MUST NOT require editing core arbiter code unless it introduces a new primitive or a new reusable policy type.

#### Scenario: 高优先级打断低优先级
- **WHEN** 一个 Player source request 和一个 Auto source request 对同一 body 或 claim group 产生冲突
- **AND** 配置声明 higher-priority interrupts lower-priority
- **THEN** 仲裁层接受高优先级 request
- **AND** 低优先级 request 以 interrupted reason 结束

#### Scenario: 新增策略类型
- **WHEN** 一个新行为需要当前 registry 不支持的全新冲突策略
- **THEN** 系统 MAY 扩展 reusable policy registry
- **AND** 该扩展 MUST include tests proving existing policies remain unchanged

### Requirement: 仲裁读取 final Component 结果
系统 SHALL 在行为仲裁和计划中读取 `GameWorld` final Component result 作为规则输入。仲裁层 MUST NOT query `RuntimeEffectStore`、`AbilityKind` or `EffectKind` to decide whether an action is accepted, rejected, interrupted, merged, or blocked.

#### Scenario: 运行时效果改变移动权限
- **WHEN** 一个运行时效果已经被解析为 final movement permission component result
- **AND** 行为仲裁处理一个移动 request
- **THEN** 仲裁层读取 final component result 判断是否可移动
- **AND** 仲裁层不读取 runtime effect source 数据

#### Scenario: 运行时效果改变可推动能力
- **WHEN** 一个运行时效果已经被解析为 final pushable component result
- **AND** push 或 mechanism movement request 需要判断目标是否可推动
- **THEN** 规则层读取 final component result
- **AND** 规则层不通过 ability 或 effect 名称判断

### Requirement: 执行层只消费统一结果
系统 SHALL process ordinary runtime behavior through an Action Policy Pipeline with explicit stages for action intake, `ActionSpec` lookup, tag/component gate, subject selection, target selection, registered strategy execution, claim production, claim arbitration, planning, and commit. The central arbitration stage SHALL compare candidates and claims; it MUST NOT build every behavior-specific outcome in one monolithic action interpreter. Runtime effect and runtime component/tag outputs SHALL leave strategy execution as commit proposals or effect applications, never as direct `GameWorld` final-state writes. Action strategy selection SHALL use a stable registered strategy id resolved from configuration/provider data. New strategy types MUST be added as registered strategy modules with tests, not as new branches in `StateDrivenRuleExecutionSystem` or ordinary action-name checks.

#### Scenario: 普通行为进入管线
- **WHEN** player move, auto move, mechanism push, configured wind push, debug move, debug spawn, or debug remove is submitted
- **THEN** the request enters the same pipeline stages
- **AND** each stage consumes `ActionSpec` policy data and final `GameWorld` component/tag facts
- **AND** no stage selects ordinary behavior by comparing raw action id strings

#### Scenario: 策略注册选择底层能力
- **WHEN** a ready action references a strategy id
- **THEN** the pipeline resolves the registered strategy module for that id
- **AND** the central arbitration stage does not switch on ordinary behavior names to choose the module
- **AND** unknown strategy ids fail clearly before world mutation

#### Scenario: 生成注册代码进入管线
- **WHEN** a strategy class has valid registration metadata
- **THEN** the editor/tooling generation path MAY use reflection to discover strategy metadata
- **AND** it produces deterministic explicit registration source
- **AND** server/test composition uses that generated or explicit source to build the strategy registry injected into the pipeline
- **AND** runtime rule execution is deterministic without assembly scanning or Unity editor-only APIs

#### Scenario: 仲裁器只处理候选和 claim
- **WHEN** strategies produce candidate action units and claims
- **THEN** central arbitration sorts and resolves them by priority, claim mode, merge policy, interrupt policy, and conflict policy
- **AND** it does not perform target selection, subject expansion, blocked branch matching, deferred output construction, or commit mutation directly

#### Scenario: 配置行为不修改中央类
- **WHEN** a new ordinary behavior reuses existing strategy and policy capabilities
- **THEN** implementation changes are limited to Luban data, generated config, tests, and optional documentation
- **AND** files containing central arbitration orchestration do not change for that behavior

#### Scenario: 新策略小类不修改执行系统
- **WHEN** a new low-level action strategy cannot be expressed by existing strategies
- **THEN** the developer adds a strategy class, registration metadata, config reference, and tests
- **AND** `StateDrivenRuleExecutionSystem` and central action orchestration do not gain a new behavior-specific branch

#### Scenario: Effect output does not write final state
- **WHEN** `ApplyRuntimeEffect` or future effect-driven strategy executes
- **THEN** it emits `EffectApplication` and commit proposals
- **AND** it does not call `GameWorld.SetComponent`, `GameWorld.AddTag`, `GameWorld.RemoveTag`, or runtime effect store mutation directly

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing registered strategies, tag gates, subject policies, targeting policies, blocked result policies, deferred output policies, claim policies, plan rules, commit handlers, and cost policies to be added by Luban action policy data and tests. It MUST NOT require new branches in core execution, central arbitration, planning, deferred output, or commit orchestration. If a behavior requires a new strategy, condition kind, result kind, claim kind, commit handler, or reusable policy type, the system SHALL add that capability through an explicit registered module with focused tests, not by editing a central action-name branch.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing movement strategy、mechanism-like source、tag gate、exclusive target cell claim、configured blocked result branches and connected-body subject policy
- **THEN** 开发者新增或修改 Luban action policy and blocked result policy rows
- **AND** 运行 Luban 导出生成 JSON/provider
- **AND** 添加 Unity TestFramework EditMode 覆盖
- **AND** 不修改核心 execution / central arbitration / planning / deferred output / commit orchestration code

#### Scenario: 新增冰面滑行
- **WHEN** 新增一个 `ice_slide` 行为，能够由已有 strategy module、targeting policy、blocked result policy、cost policy 和 commit policy 表达
- **THEN** 行为差异由 action policy 数据表达
- **AND** 核心规则模块不新增 `ice_slide` 名字分支
- **AND** 中央仲裁器不需要理解冰面玩法名

#### Scenario: 新增底层策略模块
- **WHEN** 新需求无法由已有 strategy modules、condition kinds、result kinds、claim kinds、commit handlers 和 reusable policies 表达
- **THEN** 系统 MAY add a new strategy module, condition kind, result kind, claim kind, commit handler, or policy type
- **AND** 该新增 MUST be treated as a core extension with explicit OpenSpec proposal, tasks, automated tests, and manual verification path
- **AND** the new module declares registration metadata with an attribute or equivalent compile-time declaration
- **AND** editor/tooling reflection generation or explicit compile-time registration emits deterministic registry code for that module
- **AND** the new module is selected by typed strategy or policy data instead of an ordinary behavior action-name branch

#### Scenario: 同策略不同行为 id 等价
- **WHEN** two ordinary action specs have different ids but identical relevant strategy, tag gate, subject, target, blocked result, deferred output, claim, plan, commit, and cost policy fields
- **THEN** they produce equivalent action units, claims, arbitration decisions, planning, commit, blocked result, handoff, and deferred output behavior for the same world state
- **AND** differences in behavior require explicit policy data differences or a different registered strategy module

#### Scenario: tag 不形成隐藏策略
- **WHEN** an entity has source, ability, state, immunity, or blocker tags
- **THEN** those tags may be used as condition inputs or filters
- **AND** rules do not infer a complete behavior strategy from tag combinations without an explicit strategy, policy field, or policy type

### Requirement: 兼容迁移现有行为
系统 SHALL migrate existing `PlayerMove`、`AutoMove`、`MechanismPush`、`DebugMove`、`DebugSpawn`、`DebugRemove` and push handoff behavior into behavior action units. During migration, compatibility adapters MAY exist, but the target architecture MUST make source-specific behavior data-driven and MUST remove ordinary parent retry as a success path.

#### Scenario: 玩家移动保持明确结果
- **WHEN** 玩家请求合法一步移动或非法远距离移动
- **THEN** 迁移后的行为结果、最终坐标、错误码、reason 和 dirty/delta behavior match the intended covered behavior
- **AND** pushable blocker cases return handoff instead of source movement success after child retry

#### Scenario: 自动移动保持一致
- **WHEN** 拥有 final `AutoMoveComponent` 和 `DirectionComponent` 的 entity 到达移动 tick
- **THEN** 迁移后的自动移动、阻挡、反弹方向和 auto move tick update match the intended covered behavior
- **AND** auto source differences are represented by source context and spec policy

#### Scenario: 机关推动保持一致
- **WHEN** 一个 entity 位于 `PushOnEnterComponent` 地格中
- **THEN** 迁移后的 mechanism source move / push behavior enters behavior action unit intake
- **AND** blocked target handling uses reject, handoff, or configured result branch instead of direct coordinate mutation

#### Scenario: 删除旧 parent retry 语义
- **WHEN** old tests or code expect `RetriesParentAfterDerivedSuccess` or equivalent parent retry success
- **THEN** they are rewritten or removed
- **AND** new tests assert source handoff completion and independent target action unit handling

### Requirement: Unity TestFramework 和手动端到端验证
系统 SHALL include Unity TestFramework EditMode tests for behavior action unit schema, arbitration policies, handoff branches, existing behavior compatibility, and ordinary behavior extension. Manual Play Mode verification SHALL confirm server-authoritative end-to-end behavior and two-client WorldDelta sync. Unity Player build MUST NOT be required.

#### Scenario: EditMode 覆盖 action unit
- **WHEN** Unity TestFramework EditMode tests run
- **THEN** they verify action spec lookup, action unit creation, handoff target creation, claim arbitration, same-claim merge, conflict rejection, priority interruption, blocked final component conditions, and no parent retry

#### Scenario: Server verification covers handoff
- **WHEN** server authoritative verification runs
- **THEN** it covers A handoff to B, B independent movement, non-pushable failure, nested handoff, and single-unit commit expectations
- **AND** it does not require Unity Player build

#### Scenario: 手动端到端验证
- **WHEN** 用户在 Play Mode 中运行服务端权威路径和两个客户端
- **AND** 玩家移动、自动移动、机关推动或 handoff 产生服务端状态变化
- **THEN** observer 客户端收到服务端 WorldDelta 后显示一致结果
- **AND** 客户端不本地决定冲突、打断、handoff 或最终权威坐标

### Requirement: WorldActionKind 删除边界
系统 SHALL remove `WorldActionKind` from the runtime action model. All ordinary behaviors MUST enter the runtime through `ActionSpecId` and action policy data. Queueing, pending, arbitration, planning, and commit APIs MUST NOT disguise a configured action as an unrelated legacy kind to obtain behavior.

#### Scenario: configured action 只携带 ActionSpecId
- **WHEN** a configured move-like action is enqueued by `ActionSpecId`
- **THEN** its primitive, source, priority, target rule, and handoff behavior are resolved from the referenced `ActionSpec`
- **AND** the runtime action does not carry `WorldActionKind`

#### Scenario: convenience enqueue methods fill spec id
- **WHEN** existing convenience methods create player move, auto move, mechanism push, debug move, debug spawn, or debug remove
- **THEN** they fill an explicit `ActionSpecId`
- **AND** the resulting request follows the same policy path as any configured action

### Requirement: Push Contact Batch State Separation
系统 SHALL store push contact batch state separately from `ActionRequest` and `ActionSpec`. Push contact batch state MUST be runtime lifecycle data that relates action units created by the same blocked step. Static action policy data MAY select blocked and handoff behavior, but it MUST NOT store per-instance child unit ids, batch completion, or owner result state.

#### Scenario: batch state 不进入 ActionSpec
- **WHEN** a blocked action creates a push contact batch
- **THEN** the batch id, child unit ids, batch status, and completion result are stored in pending state
- **AND** the referenced `ActionSpec` remains static policy data

#### Scenario: runtime request 不复制 batch state
- **WHEN** a child action unit is converted into an `ActionRequest`
- **THEN** the request carries only source state, derived id, owner id, spec id, target, direction, tick, and runtime params needed for arbitration
- **AND** the push contact batch lifecycle remains in pending state

#### Scenario: batch state does not imply propagation length policy
- **WHEN** a push contact batch creates or observes multiple derived action units
- **THEN** pending state records batch lifecycle and child unit ids
- **AND** it does not introduce a propagation-length failure policy
- **AND** raw contact count and subject member count do not become behavior limits

### Requirement: Core Extension Boundary For Multi-Contact Push
系统 SHALL treat multi-contact push propagation as a focused implementation-layer extension to the push pipeline, not as a complete transaction system or ordinary behavior config tweak. Adding multi-contact push support MUST include focused tests for existing single-child handoff compatibility, contact set discovery, multi-child push batch creation, batch success/failure aggregation, and data-driven behavior-name independence.

#### Scenario: 新增 multi-contact push 能力
- **WHEN** the system adds support for one parent action unit deriving multiple child action units in one blocked step
- **THEN** the change is implemented as focused push contact collection and pending batch support
- **AND** ordinary action names do not select batch behavior through string matching

#### Scenario: 普通行为继续数据驱动
- **WHEN** a new ordinary move-like behavior uses existing multi-contact push, blocked policy, and handoff policy support
- **THEN** it can be added through action policy data and tests
- **AND** no new core branch is required unless it introduces a new primitive or reusable policy type

### Requirement: 等价 Deferred Output 入队合并
系统 SHALL merge equivalent deferred outputs before they become queued runtime actions. Two deferred outputs are equivalent when they have the same ready tick, action spec id, direction, and resolved subject key. Equivalence MUST NOT depend on parent causality id, created tick, raw contact count, or the debug/logging dedupe string. Merged outputs MUST preserve an equivalent contribution count for diagnostics and future strength policy, but this change SHALL NOT interpret that count as force, strength, priority, or movement distance.

#### Scenario: 同 tick 同 subject 同方向合并
- **WHEN** multiple parent actions in the same tick produce deferred push outputs with the same ready tick
- **AND** those outputs have the same action spec id, direction, and resolved subject key
- **THEN** the runtime action queue contains at most one ready action for that equivalent deferred output
- **AND** different parent causality ids do not create duplicate queued actions
- **AND** the queued output records the number of equivalent contributions merged into it

#### Scenario: 不同方向不合并
- **WHEN** two deferred push outputs target the same resolved subject and ready tick
- **AND** one output direction is `Up` while the other output direction is `Down`
- **THEN** both deferred outputs remain observable as distinct queued actions
- **AND** no cancellation, interruption, or direction conflict decision is applied by deferred dedupe

#### Scenario: 不同 subject 不合并
- **WHEN** two deferred push outputs have the same ready tick, action spec id, and direction
- **AND** they resolve to different subject keys
- **THEN** both outputs remain queued independently
- **AND** multi-contact fanout to distinct downstream subjects is preserved

#### Scenario: 贡献数不改变当前强度
- **WHEN** equivalent deferred outputs are merged
- **THEN** their contribution count is retained for diagnostics and future policy
- **AND** the current runtime does not convert the contribution count into stronger push, longer movement, higher priority, or extra queued actions

### Requirement: Deferred Output 诊断摘要
系统 SHALL provide bounded diagnostics for deferred output enqueue. Diagnostics MUST show enough information to identify deferred growth without logging every repeated action in an explosive fanout scenario.

#### Scenario: 重复 deferred 输出摘要
- **WHEN** one tick produces multiple equivalent deferred outputs
- **THEN** diagnostics report raw deferred count, enqueued count, merged count, contribution count, and representative equivalent keys
- **AND** diagnostics avoid expanding every duplicate action and touched entity in the log

#### Scenario: 小规模 deferred 仍可追踪
- **WHEN** one tick produces a small number of deferred outputs
- **THEN** diagnostics still include spec id, entity id, subject key, direction, ready tick, cost, and causality sample
- **AND** the user can trace a single push chain across ticks

### Requirement: 进入推动输出配置化
系统 SHALL express `PushOnEnterComponent` output behavior through Luban Excel configuration data. The component application layer MUST NOT hard-code ordinary action spec ids such as `"mechanism_push"` to decide what behavior a PushOnEnter entity emits, and tests for configured behavior MUST use Luban-generated data rather than fallback defaults.

#### Scenario: PushOnEnter 从配置创建输出
- **WHEN** Luban Excel data declares an entity archetype with `PushOnEnter`
- **AND** it declares output action spec id and output cost ticks
- **THEN** entity construction creates `PushOnEnterComponent` from those configured values
- **AND** `ComponentApplicationRegistry` does not choose the output spec id by hard-coded string

#### Scenario: 不同行为复用 PushOnEnter 组件
- **WHEN** conveyor, wind field, or another tile-like entity uses `PushOnEnterComponent`
- **AND** each config row declares a different output action spec or cost
- **THEN** each entity emits the configured output action
- **AND** core rules do not add entity-name or action-name branches to distinguish them

#### Scenario: fallback 不作为统一数据源
- **WHEN** tests or runtime need PushOnEnter output data
- **THEN** they load Luban-generated config data
- **AND** they do not rely on fallback provider defaults to choose output spec or cost

### Requirement: 普通 action pipeline 不依赖 Pending
系统 SHALL manage action unit lifecycle through one unified state machine or equivalent centralized lifecycle module. Strategies, policy evaluators, tag gates, subject selectors, target selectors, claim arbiters, planners, and commit systems MAY request state transitions, but they MUST NOT each maintain hidden lifecycle state for waiting, retry, completion, cancellation, or failure.

#### Scenario: ready unit 进入候选构建
- **WHEN** an action unit reaches its ready tick
- **THEN** the unified lifecycle moves it into the candidate-building stage
- **AND** strategy modules may build candidates and claims without directly marking the unit completed

#### Scenario: 仲裁结果推进状态
- **WHEN** claim arbitration accepts, rejects, interrupts, or merges an action unit
- **THEN** the unified lifecycle records the explicit accepted, rejected, interrupted, or merged state
- **AND** downstream planning and commit consume that recorded state

#### Scenario: deferred output 不创建隐藏等待链
- **WHEN** a strategy or blocked policy emits deferred output
- **THEN** the unified lifecycle records that the current unit emitted finite output or completed according to policy
- **AND** it does not create hidden parent-child waiting state outside the lifecycle module

#### Scenario: 等待语义需要显式状态扩展
- **WHEN** a future behavior truly needs waiting, retry, or child-result dependency
- **THEN** the lifecycle MUST add explicit states or transitions through a separate OpenSpec change
- **AND** the implementation MUST NOT reuse removed push pending chain semantics as an implicit lifecycle state

### Requirement: 同 tick Push Vector Composition
系统 SHALL compose same-tick push contributions targeting the same resolved subject into a deterministic net push vector before those contributions become independent movement attempts. All push contributions SHALL enter the same default composition pool unless a future explicit composition-group policy is added. Composition MUST preserve contribution metadata and MUST NOT use repeated queued action count as the source of strength.

#### Scenario: 相反方向抵消
- **WHEN** the same resolved subject receives an `Up` push contribution and a `Down` push contribution for the same ready tick
- **THEN** the system computes a net vector whose vertical component is cancelled
- **AND** cancelled contributions do not both continue as independent push actions
- **AND** diagnostics preserve the contributing causality samples

#### Scenario: 同方向累加为贡献元信息
- **WHEN** the same resolved subject receives multiple same-direction push contributions for the same ready tick
- **THEN** the system keeps one composed push intent for that direction
- **AND** the composed intent records contribution count and per-direction contribution metadata
- **AND** the current behavior does not convert contribution count into extra movement distance

#### Scenario: 不同 push spec 默认合成
- **WHEN** different action specs produce push contributions for the same resolved subject and ready tick
- **THEN** those contributions enter the same default push composition pool
- **AND** the system does not require action spec grouping before composing ordinary push contributions

#### Scenario: 不同 subject 不合成
- **WHEN** two push contributions have the same ready tick and direction
- **AND** they target different resolved subjects
- **THEN** they remain separate composed intents
- **AND** one subject's contribution metadata does not affect the other subject

### Requirement: Push Energy Metadata Reservation
系统 SHALL reserve metadata fields for future push energy or strength policies without fully interpreting those fields in the first implementation. Same-direction contribution count SHALL be retained as future strength input, but it MUST NOT affect movement distance, priority, cost, or collision bypass until a future explicit strength policy is added.

#### Scenario: energy 不改变当前移动
- **WHEN** a composed push intent contains energy placeholder data
- **THEN** the push still resolves through the current one-step movement semantics
- **AND** same-direction contribution count and energy value do not produce multi-cell movement, higher priority, or collision bypass

#### Scenario: future policy has retained inputs
- **WHEN** future work introduces energy decay, strength, mass, or multi-step push
- **THEN** it can read total contribution, per-direction contribution, net vector, and causality samples from the composed push metadata
- **AND** it does not need to restore duplicate queued actions to recover contribution facts

### Requirement: Action Runtime Uses Resolved IDs
The runtime action model SHALL use resolved action and policy identifiers after configuration import. `ActionRequest`, `WorldAction`, deferred output, pending state, arbitration, planning, and commit MUST NOT require raw action spec strings to choose ordinary behavior strategy.

#### Scenario: action 输入只携带解析后 ID
- **WHEN** a configured action is enqueued from player input, auto movement, push-on-enter output, debug tools, or deferred output
- **THEN** the action carries a resolved action spec identifier
- **AND** the registry resolves that identifier to policy data without comparing ordinary behavior names in rule code

#### Scenario: deferred output 等价键不依赖 debug 字符串
- **WHEN** equivalent deferred outputs are merged
- **THEN** equivalence uses resolved spec id, ready tick, direction, and resolved subject key
- **AND** parent causality id, debug string, raw contact count, or readable action alias do not decide equivalence

#### Scenario: 未解析 ID 显式失败
- **WHEN** runtime code attempts to enqueue or resolve an action using an unknown or unresolved action identifier
- **THEN** Shared GameCore fails with a clear error
- **AND** the action is not silently treated as player move, player push, mechanism push, or debug move

### Requirement: Ordinary Behavior String Branch Ban
Runtime action execution SHALL reject or flag new ordinary behavior strategy branches that compare raw action names, policy names, entity names, or string tag combinations. The ban applies to action intake after registry lookup, arbitration, planning, commit, pending, deferred output, and result application.

#### Scenario: 新普通行为只改数据和测试
- **WHEN** a new ordinary behavior uses existing primitive, source, target, blocked result, subject, handoff, conflict, merge, interrupt, plan, commit, and cost policies
- **THEN** it is added through configuration/provider data and tests
- **AND** no core runtime branch compares its readable behavior name

#### Scenario: 表现层字符串例外
- **WHEN** client animation, editor UI, sandbox authoring, logs, or diagnostics need readable names
- **THEN** they MAY use readable aliases or debug names
- **AND** those aliases do not affect server-authoritative arbitration, planning, commit, pending, or final coordinates

### Requirement: Targeting Policy Data Boundary
系统 SHALL express ordinary action target selection through explicit targeting policy data. `ActionSpec` MUST reference a resolved targeting policy and target selector id; formal Luban action authoring and new tests MUST NOT use `ActionTargetRule` as the target algorithm entry. Compatible migration rules MAY map old `ActionTargetRule` values to targeting policies only inside explicit compatibility test or import boundaries. Runtime execution MUST NOT add ordinary behavior-name branches or new `ActionTargetRule` enum members to choose target shape, direction source, filter, ordering, selector class, or empty-target handling.

#### Scenario: action 引用 targeting policy
- **WHEN** player move, auto move, mechanism push, debug move, or configured front multi-target behavior enters the rules layer
- **THEN** its target selection is resolved from `ActionSpec` targeting policy data
- **AND** the rule layer does not compare raw action names to choose target shape

#### Scenario: legacy target rule 只作迁移入口
- **WHEN** an existing action still uses `ActionTargetRule`
- **THEN** the registry or adapter maps it into equivalent targeting policy semantics
- **AND** new ordinary behavior does not require adding a new central `ActionTargetRule` branch
- **AND** new target algorithms are introduced as selector classes or extensions and then mapped from configuration by selector id
- **AND** this legacy mapping is not used by formal Luban source tables after this change

#### Scenario: selector class comes from config
- **WHEN** an action policy references a targeting selector id
- **THEN** the runtime resolves that id to a registered target selector class or extension
- **AND** changing the action alias does not change selector behavior

#### Scenario: 新目标算法不改枚举
- **WHEN** a new target algorithm such as cross, radius, ray, adjacent entity, or line area is added
- **THEN** the developer adds a selector module, selector id registration, config rows, and tests
- **AND** the change does not add a new `ActionTargetRule` enum member

### Requirement: Targeting Config Import Resolution
系统 SHALL resolve targeting selector, targeting spec, and target filter authoring names into runtime identifiers or enums before authoritative rules consume them. Runtime action arbitration, execution, planning, and commit MUST NOT compare raw targeting names, selector names, filter names, tag strings, or action aliases to decide target behavior.

#### Scenario: 配置名在 provider 边界解析
- **WHEN** Luban data declares `targeting_selector_id`, `targeting_id`, `filter_id`, target tags, or readable target policy names
- **THEN** the provider resolves them into runtime identifiers, enums, or typed tag values
- **AND** `Shared.DG.GameCore` rule modules consume only the resolved values

#### Scenario: 未知 targeting 引用显式失败
- **WHEN** an action policy references an unknown targeting, selector, or filter id
- **THEN** registry construction fails with a clear error
- **AND** the action is not silently treated as player move, direction cell, self, or front entities

### Requirement: Target Filter Reads Final Facts
Target filter policy SHALL evaluate final `GameWorld` component, tag, and spatial facts only. It MUST NOT read RuntimeEffect source data, Unity presentation state, Fantasy session state, entity names, action names, or debug aliases to decide whether a target is included.

#### Scenario: final component 过滤
- **WHEN** a targeting policy filters for pushable positioned entities
- **THEN** the filter reads final `PositionComponent` and `PushableComponent` facts
- **AND** it does not inspect which RuntimeEffect or authoring source produced those final facts

#### Scenario: tag 过滤不替代策略字段
- **WHEN** a target has source, ability, state, immunity, or blocker tags
- **THEN** target filtering may include or exclude the target using resolved tag facts
- **AND** those tags do not choose target shape, subject policy, blocked policy, handoff policy, or commit behavior

### Requirement: Targeting Verification
系统 SHALL verify targeting policy data through OpenSpec validation, Shared build, server verification, and Unity TestFramework EditMode tests. End-to-end target hit behavior SHALL remain a manual Play Mode / two-client verification step.

#### Scenario: automated targeting validation
- **WHEN** automated validation runs
- **THEN** it includes `openspec validate`, Shared GameCore build, server authoritative verification, and Unity EditMode tests for targeting data import, selector registry mapping, self target, direction cell, target coord, filter evaluation, deterministic ordering, and unknown-id failure

#### Scenario: manual targeting validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** target selection results are observed only through server-produced snapshot/delta
- **AND** both clients converge to the same final server state for player move, auto move, and mechanism push

### Requirement: Action Execution Emits EffectApplications
系统 SHALL allow action execution to emit `EffectApplication` outputs from `ActionContext`, `TargetData[]`, and `EffectSpec` references. `ApplyRuntimeEffect` and future `ApplyEffectsToTargets` execution MUST produce effect applications or commit proposals without directly mutating final `GameWorld` Component/tag state.

#### Scenario: ApplyRuntimeEffect uses TargetData
- **WHEN** a configured action uses `ApplyRuntimeEffect`
- **AND** targeting resolves three target entities
- **THEN** execution emits three `EffectApplication` values using the configured effect spec
- **AND** each target receives its own target binding and causality metadata

#### Scenario: Execution does not bypass commit
- **WHEN** an effect application is produced
- **THEN** execution routes it to commit output
- **AND** execution does not call runtime effect store, component setters, tag setters, or world mutation APIs directly

### Requirement: Effect Spec References Are Data Driven
系统 SHALL resolve effect spec references from Luban action/effect configuration data. A new ordinary effect-driven action that reuses existing targeting, payload, duration, stack, and commit policies MUST be addable through config/provider data and tests without editing core action execution or arbitration flow.

#### Scenario: New temporary pushable action changes data only
- **WHEN** a new action grants Pushable for a timed duration to its target
- **THEN** the developer adds or updates action/effect config and tests
- **AND** no core branch compares the action name or effect name to choose behavior

#### Scenario: Effect id resolves through Luban registry
- **WHEN** an action references an effect spec id
- **THEN** the id resolves through the Luban-backed effect spec registry
- **AND** runtime rules do not compare raw effect name strings to choose payload, duration, or stack behavior

#### Scenario: Unknown effect id fails clearly
- **WHEN** runtime code attempts to execute an action referencing an unknown effect spec id
- **THEN** Shared GameCore fails the action with a clear reason
- **AND** it does not silently apply a default runtime effect

### Requirement: Effect Outputs Do Not Replace Claim Arbitration
系统 SHALL keep movement, push, body movement, target cell reservation, spawn, remove, direction changes, and other world mutations inside registered action, claim, planning, and commit boundaries. Effect applications MAY change final component/tag/stat-like facts that later action arbitration reads, but they MUST NOT directly accept or reject movement claims. Commit proposal execution SHALL be handled by registered commit handlers so new commit semantics do not require central commit orchestration branches.

#### Scenario: Immobile effect affects later movement
- **WHEN** an action applies an immobile effect to an entity
- **AND** a later move action is processed for that entity
- **THEN** movement is rejected through normal action arbitration reading final movement permission
- **AND** the effect application itself does not perform movement arbitration

#### Scenario: Effect does not reserve target cell
- **WHEN** an effect is applied to a target entity in a cell
- **THEN** the effect does not reserve, occupy, or move into any target cell by itself
- **AND** target cell conflicts remain owned by action claims and commit validation

#### Scenario: 新 commit handler 不修改中心提交流程
- **WHEN** a future accepted action needs a new commit proposal semantic
- **THEN** the developer adds a commit proposal payload, handler registration, and tests
- **AND** the central commit resolver does not add a new behavior-name or action-name branch
- **AND** unknown commit handlers fail clearly before world mutation

### Requirement: Authoritative Action Context

系统 SHALL represent each server-authoritative action with an explicit `ActionContext` or equivalent runtime context. The context MUST distinguish instigator, source entity, optional causer, subject entry, target hint, direction, spec id, owner action id, causality, created tick, ready tick, and cost tick. The context MUST NOT decide behavior policy by itself and MUST NOT create implicit parent/child waiting.

#### Scenario: Player move context
- **WHEN** a player move input enters the authoritative action queue
- **THEN** the created context records the player as instigator, source entity, and subject entry
- **AND** it records the target hint or direction supplied by input
- **AND** it resolves behavior through `ActionSpec` rather than action-name branches

#### Scenario: AutoMove self push context
- **WHEN** an AutoMove entity reaches its cost tick
- **THEN** the created context records the AutoMove entity as instigator, source entity, and subject entry
- **AND** it records direction from final `DirectionComponent`
- **AND** it does not create pending parent/child state to remember the source

#### Scenario: Context is not world state
- **WHEN** arbitration needs current position, tags, components, blocking, or pushability
- **THEN** it reads those facts from final `GameWorld` state at the current tick
- **AND** it does not reuse stale world facts embedded inside `ActionContext`

### Requirement: TargetData Targeting Output

系统 SHALL express targeting/query results as `TargetData` or an equivalent finite target-data set before claim generation. TargetData MUST be a read-only candidate target description, not a commit, not a claim, and not a final result. The first supported target data policies MUST include Self, DirectionCell, and a front-cell/front-entity query sufficient for moving all objects in front of a source.

#### Scenario: Self targeting
- **WHEN** an action uses Self targeting
- **THEN** targeting emits one TargetData item for the action subject entry
- **AND** later execution may turn that TargetData into claims according to configured strategy

#### Scenario: DirectionCell targeting
- **WHEN** an action uses DirectionCell targeting with a valid direction
- **THEN** targeting emits one TargetData item for the adjacent cell in that direction
- **AND** the TargetData records the source query id and direction

#### Scenario: Front entities targeting
- **WHEN** an action uses the minimal front-entities targeting policy
- **THEN** targeting emits a finite ordered TargetData set for matching entities or occupied cells in front of the source
- **AND** ordering is deterministic and does not depend on dictionary enumeration order

### Requirement: ExecutionOutput Contract

系统 SHALL convert `ActionContext` plus TargetData into `ExecutionOutput` or an equivalent structured execution result before claim arbitration. ExecutionOutput MAY include claim candidates, blocked outcome candidates, deferred output candidates, commit candidates, result owner mapping, and success policy. ExecutionOutput MUST NOT directly mutate `GameWorld`.

#### Scenario: Move execution creates claim candidates
- **WHEN** a move-compatible execution consumes one or more TargetData items
- **THEN** it emits move claim candidates for the resolved subject or target subjects
- **AND** it does not directly change entity positions

#### Scenario: Blocked output stays structured
- **WHEN** execution detects or receives a blocked outcome candidate
- **THEN** it emits structured blocked/deferred/result data for the policy layer
- **AND** it does not hardcode push, bounce, or reject behavior by action id

#### Scenario: Commit still owns world mutation
- **WHEN** ExecutionOutput contains commit candidates or accepted claims
- **THEN** planning and commit validation still decide whether `GameWorld` changes
- **AND** failed validation prevents mutation even if execution produced candidates

### Requirement: Multi Target Success Policy Entry

系统 SHALL provide an explicit success policy entry for multi-target execution. The default policy MUST be all-or-nothing for required claims. Partial success MUST require explicit policy support and MUST NOT occur accidentally because some generated claims committed while others failed.

#### Scenario: Default all-or-nothing
- **WHEN** one action produces required claims for multiple TargetData items
- **AND** any required claim fails arbitration, planning, or commit
- **THEN** the action result is not reported as overall success
- **AND** no partial success is treated as the default behavior

#### Scenario: Partial success requires policy
- **WHEN** an action spec or execution policy does not explicitly allow partial success
- **AND** only some target claims can succeed
- **THEN** the system rejects or fails the unit according to all-or-nothing rules
- **AND** it does not silently report partial success

#### Scenario: Partial policy has data entry
- **WHEN** a future action needs partial success
- **THEN** the success behavior is represented through explicit success policy data and per-target result mapping
- **AND** adding that behavior does not require central action-name branching

