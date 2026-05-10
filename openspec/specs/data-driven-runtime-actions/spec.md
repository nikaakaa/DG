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
系统 SHALL 用行为原语表达执行类型，用 source context 表达行为来源。`Move`、`Spawn`、`Remove`、`SetComponentResult`、`ApplyRuntimeEffect` 和 `HandoffMove` SHALL be primitives or primitive candidates; `Player`、`Auto`、`Mechanism`、`RuntimeResult`、`Debug` 和 `Handoff` SHALL be source / policy context, not separate execution branches for the same primitive.

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
系统 SHALL keep execution focused on ready action units, accepted claims, plans, commit proposals, commit results, dirty state, pending state transitions, push contact batch transitions, and owner action results. Execution MUST NOT branch on ordinary business behavior names such as player move, auto move, mechanism push, wind push, trap pull, or ice slide.

#### Scenario: 执行统一 Move
- **WHEN** player move, auto move, mechanism push, or configured wind push reaches its ready tick
- **THEN** execution sends the ready action unit through the unified action pipeline
- **AND** execution does not switch by behavior name to decide movement rules

#### Scenario: 调试行为使用配置
- **WHEN** debug move, debug spawn, or debug remove is submitted
- **THEN** they also enter through their corresponding `ActionSpec`
- **AND** debug-specific permission and target policy are expressed as action data or explicit debug source policy

#### Scenario: Pending continuation uses unified execution
- **WHEN** a waiting parent action unit becomes ready to continue
- **THEN** execution treats it like another ready action unit
- **AND** it is not advanced through a push-front special branch

#### Scenario: push contact batch uses unified execution
- **WHEN** one blocked action unit creates a push contact batch with multiple child units
- **THEN** execution schedules those child units through the same ready action unit path
- **AND** execution does not add a behavior-name branch to move all child subjects directly

#### Scenario: push-specific handoff interpretation stays out of execution
- **WHEN** a blocked move discovers multiple external push contacts
- **THEN** contact-to-handoff-subject interpretation happens in the action policy / arbitration layer
- **AND** execution only observes derived action units, pending transitions, commit results, and owner action results
- **AND** execution does not infer child unit count from raw contact count

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing primitives and policies to be added by Luban action policy data and tests. It MUST NOT require new branches in core execution, arbitration, planning, pending state, or commit orchestration.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing `Move` primitive、mechanism-like source、exclusive target cell claim 和 blocked tag policy
- **THEN** 开发者新增或修改 Luban Excel action policy row
- **AND** 运行 Luban 导出生成 JSON/provider
- **AND** 添加 Unity TestFramework EditMode 覆盖
- **AND** 不修改核心 execution / arbitration orchestration code

#### Scenario: 新增底层原语
- **WHEN** 新需求无法由已有 primitive 和 reusable policies 表达
- **THEN** 系统 MAY add a new primitive or policy type
- **AND** 该新增 MUST be treated as core extension with explicit tests and proposal/task coverage

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
系统 SHALL allow ordinary ready actions and structured deferred outputs to execute through the action pipeline without a `PendingRuleStateStore`. Pending state MUST NOT be required for push continuation, PushOnEnter output, or deferred output re-entry.

#### Scenario: Server runner executes without pending push chain
- **WHEN** a server tick drains ready player, mechanism, auto, debug, or deferred actions
- **THEN** the main rule execution path processes them without creating pending child units for push continuation
- **AND** downstream push continuation is represented by deferred output
- **AND** source action results do not wait for downstream deferred result success

#### Scenario: Future waiting action is explicit
- **WHEN** a future behavior needs parent action result to wait for child action results
- **THEN** that behavior requires an explicit waiting-action proposal and policy
- **AND** it MUST NOT reuse legacy push pending chain as an implicit default path

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

