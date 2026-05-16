## ADDED Requirements
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
系统 SHALL keep movement, push, body movement, target cell reservation, spawn, remove, and direction changes inside the existing claim/planning/commit boundaries. Effect applications MAY change final component/tag/stat-like facts that later action arbitration reads, but they MUST NOT directly accept or reject movement claims.

#### Scenario: Immobile effect affects later movement
- **WHEN** an action applies an immobile effect to an entity
- **AND** a later move action is processed for that entity
- **THEN** movement is rejected through normal action arbitration reading final movement permission
- **AND** the effect application itself does not perform movement arbitration

#### Scenario: Effect does not reserve target cell
- **WHEN** an effect is applied to a target entity in a cell
- **THEN** the effect does not reserve, occupy, or move into any target cell by itself
- **AND** target cell conflicts remain owned by action claims and commit validation
