## MODIFIED Requirements

### Requirement: ComponentStateResolver 合成边界
系统 SHALL provide a `ComponentStateResolver` that reads static sources and active runtime sources, computes final Component results, and applies only changed final results to `GameWorld`. The resolver orchestration SHALL support registered component result resolvers keyed by stable result ids or equivalent contribution payload identifiers. The first slice SHALL include `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, movement permission results, and source-based `WorldTag` results; it MUST NOT resolve `PositionComponent`, `DirectionComponent`, `PlayerControlComponent`, or runtime tick counters. New runtime component result types MUST be added by resolver modules, payload mapping, config/provider data, and tests rather than by editing a central result-kind switch.

#### Scenario: Included components are resolved
- **WHEN** static and runtime sources are resolved for an entity
- **THEN** final `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, and movement permission results are produced from their active sources
- **AND** changed final results are applied to `GameWorld`

#### Scenario: Commit value components are excluded
- **WHEN** the resolver processes an entity
- **THEN** it does not resolve `PositionComponent`
- **AND** it does not resolve `DirectionComponent`
- **AND** it does not resolve `PlayerControlComponent`
- **AND** movement and direction changes remain owned by rule commit paths

#### Scenario: Tag results are source resolved
- **WHEN** static tags and runtime tag effects both contribute `WorldTag` values to an entity
- **THEN** final `TagSetComponent` is computed from active tag sources
- **AND** removing one runtime tag source does not remove a static tag or another runtime tag source

#### Scenario: 新 runtime component result 不修改 resolver 主流程
- **WHEN** a new runtime component result type is introduced
- **THEN** the developer adds source payload mapping, a result resolver module, registration metadata, config/provider data, and Unity TestFramework EditMode tests
- **AND** `ComponentStateResolver` collection and orchestration code does not gain a new result-specific branch
- **AND** unknown result ids fail clearly before final component mutation

### Requirement: EffectSpec 静态定义
系统 SHALL define `EffectSpec` as a Luban-backed static definition table for runtime effects that can contribute final Component/tag results. An `EffectSpec` MUST declare effect id, effect kind or result payload id, target binding, duration policy, stack policy, remove policy, payload data, and optional presentation cue id. Static effect definitions MUST NOT store runtime action ids, target ids, start ticks, expire ticks, or active stack state. Effect payload import SHALL resolve authoring names into registered component result ids before authoritative rules consume them.

#### Scenario: EffectSpec 不保存运行时状态
- **WHEN** an effect definition grants temporary pushability for five ticks
- **THEN** the `EffectSpec` declares pushable payload, timed duration policy, and stack policy
- **AND** it does not store the concrete target entity, created tick, expire tick, or runtime effect instance id

#### Scenario: EffectSpec 来自 Luban 独立表
- **WHEN** first-slice effect configuration is loaded
- **THEN** effect definitions are read from Luban-generated `effect_spec` data
- **AND** the main runtime path does not rely on hand-written built-in effect specs as the authoritative configuration source

#### Scenario: EffectSpec payload 受注册表约束
- **WHEN** effect specs are loaded
- **THEN** every payload id resolves to a registered component result resolver or explicit tag result resolver
- **AND** unknown payload ids fail clearly during provider/registry construction
- **AND** effects do not describe Position, Direction, PlayerControl, health, mass, or other Attribute/Stat mutation unless a separate proposal adds those result resolvers

### Requirement: Effect Resolver Boundary
系统 SHALL keep final Component/tag resolution owned by `ComponentStateResolver` or an equivalent final-result resolver. Runtime effect data MAY contribute sources, but Rules, ActionArbiter, RulePlanner, and CommitResolver movement validation MUST read only final `GameWorld` Component/tag results when deciding movement, pushability, blocking, or permissions. New resolver modules SHALL plug into the final-result resolver registry and MUST NOT require rule modules to inspect runtime effect state.

#### Scenario: Runtime effect changes rules through final result
- **WHEN** a runtime effect grants `PushableComponent` to an entity
- **AND** a later push action evaluates that entity
- **THEN** the push rule reads the final `PushableComponent`
- **AND** the rule does not query `RuntimeEffectStore`, `EffectSpec`, `EffectApplication`, or `EffectKind`

#### Scenario: Effect cannot move entity directly
- **WHEN** an effect payload would imply movement or push behavior
- **THEN** it must create or configure a future action/effect path rather than directly changing PositionComponent
- **AND** actual movement still goes through action, claim, arbitration, planning, and commit

#### Scenario: Resolver output is composable
- **WHEN** static sources and multiple runtime sources contribute the same registered result id
- **THEN** the registered resolver computes one deterministic final component result
- **AND** removing one runtime source leaves the final result active when another source still contributes it

### Requirement: Effect Application Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for effect application, commit, runtime store lifecycle, resolver final results, stack policy, duration expiry, registered resolver extension, and rule-layer isolation. Unity Player build MUST NOT be required.

#### Scenario: Automated validation
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative verification, and Unity EditMode tests for applying, stacking, expiring, and removing first-slice effects
- **AND** tests prove rules read final Component/tag results rather than effect runtime state

#### Scenario: Registered resolver validation
- **WHEN** a test-only component result resolver is registered
- **THEN** an effect or runtime source can contribute that result through config/provider data
- **AND** final state settlement occurs without modifying central resolver orchestration
- **AND** unknown resolver ids fail before final state mutation

#### Scenario: Manual end-to-end validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** applying and expiring temporary pushable, immobile, auto move, port, tag, or test registered component effects converges to the same server final state on both clients
- **AND** the client does not locally decide effect hit, active state, expiry, stack behavior, or final component result
