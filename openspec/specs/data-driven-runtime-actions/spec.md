# data-driven-runtime-actions Specification

## Purpose
TBD - created by archiving change refactor-data-driven-runtime-actions. Update Purpose after archive.
## Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置、运行时行为输入和运行时行为单元分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划、提交策略和默认 cost 来源；`ActionRequest` SHALL 只携带一次运行时输入的 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params；action unit state SHALL 保存 ready tick、lifecycle、parent / derived 关系、retry 和 pending 结果。运行时行为输入 MUST NOT 复制 `ActionSpec` 的静态策略，也 MUST NOT 承担 pending 生命周期状态。

#### Scenario: 运行时请求引用配置
- **WHEN** player move, auto move, mechanism push, debug move, debug spawn, or debug remove is created
- **THEN** the runtime input references an `ActionSpec`
- **AND** the runtime input carries only request-specific source, target, direction, tick, client tick, source state, and runtime params
- **AND** action unit state carries ready tick, lifecycle, parent / derived ids, retry count, and pending status
- **AND** runtime input does not copy `ActionSpec` claim, conflict, interrupt, merge, plan, commit, or default cost policy

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
系统 SHALL keep execution focused on ready action units, accepted claims, plans, commit proposals, commit results, dirty state, pending state transitions, and owner action results. Execution MUST NOT branch on ordinary business behavior names such as player move, auto move, mechanism push, wind push, trap pull, or ice slide.

#### Scenario: 执行统一 Move
- **WHEN** player move, auto move, mechanism push, or configured wind push reaches its ready tick
- **THEN** execution sends the ready action unit through the unified action pipeline
- **AND** execution does not switch by behavior name to decide movement rules

#### Scenario: 调试行为使用配置
- **WHEN** debug move, debug spawn, or debug remove is submitted
- **THEN** they also enter through their corresponding `ActionSpec`
- **AND** debug-specific permission and target policy are expressed as action data or explicit debug source policy

#### Scenario: Pending retry uses unified execution
- **WHEN** a waiting parent action unit becomes ready to retry
- **THEN** execution treats it like another ready action unit
- **AND** it is not advanced through a push-front special branch

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing primitives and policies to be added by data configuration, registry data, and tests. It MUST NOT require new branches in core execution, arbitration, planning, or commit orchestration.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing `Move` primitive、mechanism-like source、exclusive target cell claim 和 blocked tag policy
- **THEN** 开发者新增或修改行为配置
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

