## MODIFIED Requirements
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

### Requirement: 执行层只消费统一结果
系统 SHALL process ordinary runtime behavior through an Action Policy Pipeline with explicit stages for action intake, `ActionSpec` lookup, tag/component gate, subject selection, target selection, registered strategy execution, claim production, claim arbitration, planning, and commit. The central arbitration stage SHALL compare candidates and claims; it MUST NOT build every behavior-specific outcome in one monolithic action interpreter. Runtime effect and runtime component/tag outputs SHALL leave strategy execution as commit proposals or effect applications, never as direct `GameWorld` final-state writes.

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

#### Scenario: Effect output does not write final state
- **WHEN** `ApplyRuntimeEffect` or future effect-driven strategy executes
- **THEN** it emits `EffectApplication` and commit proposals
- **AND** it does not call `GameWorld.SetComponent`, `GameWorld.AddTag`, `GameWorld.RemoveTag`, or runtime effect store mutation directly
