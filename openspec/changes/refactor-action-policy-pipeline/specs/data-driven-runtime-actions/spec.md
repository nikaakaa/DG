## MODIFIED Requirements

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing primitives, strategy modules, tag gates, subject policies, target rules, blocked result policies, deferred output policies, claim policies, plan rules, commit rules, and cost policies to be added by Luban action policy data and tests. It MUST NOT require new branches in core execution, central arbitration, planning, deferred output, or commit orchestration. If a behavior requires a new primitive, condition kind, result kind, claim kind, or reusable strategy type, the system SHALL add that capability through an explicit registered strategy or policy module with focused tests, not by editing a central action-name branch.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing `Move` primitive、mechanism-like source、tag gate、exclusive target cell claim、configured blocked result branches and connected-body subject policy
- **THEN** 开发者新增或修改 Luban action policy and blocked result policy rows
- **AND** 运行 Luban 导出生成 JSON/provider
- **AND** 添加 Unity TestFramework EditMode 覆盖
- **AND** 不修改核心 execution / central arbitration / planning / deferred output / commit orchestration code

#### Scenario: 新增冰面滑行
- **WHEN** 新增一个 `ice_slide` 行为，能够由已有 primitive、strategy module、target rule、blocked result policy、cost policy 和 commit policy 表达
- **THEN** 行为差异由 action policy 数据表达
- **AND** 核心规则模块不新增 `ice_slide` 名字分支
- **AND** 中央仲裁器不需要理解冰面玩法名

#### Scenario: 新增底层策略模块
- **WHEN** 新需求无法由已有 primitive、condition kinds、result kinds、claim kinds、strategy modules 和 reusable policies 表达
- **THEN** 系统 MAY add a new primitive, condition kind, result kind, claim kind, strategy module, or policy type
- **AND** 该新增 MUST be treated as a core extension with explicit OpenSpec proposal, tasks, automated tests, and manual verification path
- **AND** the new module declares registration metadata with an attribute or equivalent compile-time declaration
- **AND** editor/Roslyn generation emits explicit registry code for that module
- **AND** the new module is selected by typed strategy or policy data instead of an ordinary behavior action-name branch

#### Scenario: 同策略不同行为 id 等价
- **WHEN** two ordinary action specs have different ids but identical relevant strategy, tag gate, subject, target, blocked result, deferred output, claim, plan, commit, and cost policy fields
- **THEN** they produce equivalent action units, claims, arbitration decisions, planning, commit, blocked result, handoff, and deferred output behavior for the same world state
- **AND** differences in behavior require explicit policy data differences or a different registered strategy module

#### Scenario: tag 不形成隐藏策略
- **WHEN** an entity has source, ability, state, immunity, or blocker tags
- **THEN** those tags may be used as condition inputs or filters
- **AND** rules do not infer a complete behavior strategy from tag combinations without an explicit strategy, policy field, or policy type

### Requirement: Action Policy Pipeline
系统 SHALL process ordinary runtime behavior through an Action Policy Pipeline with explicit stages for action intake, `ActionSpec` lookup, tag/component gate, subject selection, target selection, registered strategy execution, claim production, claim arbitration, planning, and commit. The central arbitration stage SHALL compare candidates and claims; it MUST NOT build every behavior-specific outcome in one monolithic action interpreter.

#### Scenario: 普通行为进入管线
- **WHEN** player move, auto move, mechanism push, configured wind push, debug move, debug spawn, or debug remove is submitted
- **THEN** the request enters the same pipeline stages
- **AND** each stage consumes `ActionSpec` policy data and final `GameWorld` component/tag facts
- **AND** no stage selects ordinary behavior by comparing raw action id strings

#### Scenario: 策略注册选择底层能力
- **WHEN** a ready action references a strategy or primitive key
- **THEN** the pipeline resolves the registered strategy module for that key
- **AND** the central arbitration stage does not switch on ordinary behavior names to choose the module

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

### Requirement: 统一 Action Unit 状态机
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
