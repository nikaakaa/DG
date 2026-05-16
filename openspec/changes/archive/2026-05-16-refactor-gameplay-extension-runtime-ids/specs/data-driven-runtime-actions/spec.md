## MODIFIED Requirements
### Requirement: 执行层只消费统一结果
系统 SHALL process ordinary runtime behavior through an Action Policy Pipeline with explicit stages for action intake, `ActionSpec` lookup, tag/component gate, subject selection, target selection, registered strategy execution, claim production, claim arbitration, planning, and commit. The central arbitration stage SHALL compare candidates and claims; it MUST NOT build every behavior-specific outcome in one monolithic action interpreter. Runtime effect and runtime component/tag outputs SHALL leave strategy execution as commit proposals or effect applications, never as direct `GameWorld` final-state writes. Action strategy selection SHALL use a stable registered strategy id resolved from configuration/provider data. New strategy types MUST be added as registered strategy modules with tests, not as new `ActionPrimitive` members, new branches in `StateDrivenRuleExecutionSystem`, or ordinary action-name checks.

#### Scenario: 普通行为进入管线
- **WHEN** player move, auto move, mechanism push, configured wind push, debug move, debug spawn, or debug remove is submitted
- **THEN** the request enters the same pipeline stages
- **AND** each stage consumes `ActionSpec` policy data and final `GameWorld` component/tag facts
- **AND** no stage selects ordinary behavior by comparing raw action id strings

#### Scenario: 策略注册选择底层能力
- **WHEN** a ready action references a strategy id
- **THEN** the pipeline resolves the registered strategy module for that id
- **AND** the central arbitration stage does not switch on ordinary behavior names or `ActionPrimitive` enum members to choose the module

#### Scenario: 生成注册代码进入管线
- **WHEN** a strategy class has valid registration metadata
- **THEN** the editor/Roslyn generation path produces explicit registration source
- **AND** server/test composition uses that generated source to build the strategy registry injected into the pipeline
- **AND** the pipeline behavior is deterministic without runtime reflection or Unity editor-only scanning

#### Scenario: 仲裁器只处理候选和 claim
- **WHEN** strategies produce candidate action units and claims
- **THEN** central arbitration sorts and resolves them by priority, claim mode, merge policy, interrupt policy, and conflict policy
- **AND** it does not perform target selection, subject expansion, blocked branch matching, deferred output construction, or commit mutation directly

#### Scenario: 配置行为不修改中央类
- **WHEN** a new ordinary behavior reuses existing strategy and policy capabilities
- **THEN** implementation changes are limited to Luban data, generated config, tests, and optional documentation
- **AND** files containing central arbitration orchestration do not change for that behavior

#### Scenario: 新策略不新增 ActionPrimitive
- **WHEN** a new bottom-level action strategy such as `swap_entities` or `shockwave_push` is needed
- **THEN** the developer adds a strategy module, strategy id registration, config rows, and tests
- **AND** the change does not add a new `ActionPrimitive` enum member
- **AND** runtime strategy selection uses the registered strategy id

#### Scenario: Effect output does not write final state
- **WHEN** `ApplyRuntimeEffect` or future effect-driven strategy executes
- **THEN** it emits `EffectApplication` and commit proposals
- **AND** it does not call `GameWorld.SetComponent`, `GameWorld.AddTag`, `GameWorld.RemoveTag`, or runtime effect store mutation directly

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing strategy modules, tag gates, subject policies, targeting policies, blocked result policies, deferred output policies, claim policies, plan rules, commit handlers, and cost policies to be added by Luban action policy data and tests. It MUST NOT require new branches in core execution, central arbitration, planning, deferred output, or commit orchestration. If a behavior requires a new reusable strategy, target selector, condition, claim, commit handler, or policy module, the system SHALL add that capability through a stable id and explicit registered module with focused tests, not by editing a central action-name branch or gameplay-extension enum.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing move strategy、mechanism-like source、tag gate、exclusive target cell claim、configured blocked result branches and connected-body subject policy
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
- **WHEN** 新需求无法由已有 condition kinds、claim kinds、strategy modules 和 reusable policies 表达
- **THEN** 系统 MAY add a new condition kind, claim kind, strategy module, or policy type
- **AND** 该新增 MUST be treated as a core extension with explicit OpenSpec proposal, tasks, automated tests, and manual verification path
- **AND** the new module declares registration metadata with an attribute or equivalent compile-time declaration
- **AND** editor/Roslyn generation emits explicit registry code for that module
- **AND** the new module is selected by stable strategy or policy id instead of an ordinary behavior action-name branch or new `ActionPrimitive`

#### Scenario: 同策略不同行为 id 等价
- **WHEN** two ordinary action specs have different ids but identical relevant strategy, tag gate, subject, target, blocked result, deferred output, claim, plan, commit, and cost policy fields
- **THEN** they produce equivalent action units, claims, arbitration decisions, planning, commit, blocked result, handoff, and deferred output behavior for the same world state
- **AND** differences in behavior require explicit policy data differences or a different registered strategy module

#### Scenario: tag 不形成隐藏策略
- **WHEN** an entity has source, ability, state, immunity, or blocker tags
- **THEN** those tags may be used as condition inputs or filters
- **AND** rules do not infer a complete behavior strategy from tag combinations without an explicit strategy, policy field, or policy type

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

### Requirement: Effect Outputs Do Not Replace Claim Arbitration
系统 SHALL keep movement, push, body movement, target cell reservation, spawn, remove, direction changes, and other world mutations inside registered action, claim, planning, and commit boundaries. Effect applications MAY change final component/tag/stat-like facts that later action arbitration reads, but they MUST NOT directly accept or reject movement claims. Commit proposal execution SHALL be handled by registered commit handlers keyed by stable commit id so new commit semantics do not require central commit orchestration branches or `CommitProposalKind` enum authoring.

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
- **WHEN** a new world mutation such as swap entities is required
- **THEN** the developer adds a commit handler module, commit id registration, payload, and tests
- **AND** `CommitResolver` dispatches by stable commit id
- **AND** the change does not add a new `CommitProposalKind` enum member
