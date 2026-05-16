## MODIFIED Requirements
### Requirement: ComponentStateResolver 合成边界
系统 SHALL provide a `ComponentStateResolver` that reads static sources and active runtime sources, computes final Component results, and applies only changed final results to `GameWorld`. The resolver orchestration SHALL support registered component result resolvers keyed by stable result ids or equivalent contribution payload identifiers. The first slice SHALL include `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, movement permission results, and source-based `WorldTag` results; it MUST NOT resolve `PositionComponent`, `DirectionComponent`, `PlayerControlComponent`, or runtime tick counters. New runtime component result types MUST be added by resolver modules, payload mapping, config/provider data, and tests rather than by editing a central result-kind switch or adding `ComponentResultKind` enum members.

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
- **WHEN** a new final component result type is added
- **THEN** the developer adds a component result resolver module, result id registration, payload mapper, and tests
- **AND** `ComponentStateResolver` collection and orchestration code does not gain a new result-specific branch
- **AND** the change does not add a new `ComponentResultKind` enum member

### Requirement: EffectSpec 静态定义
系统 SHALL define `EffectSpec` as a Luban-backed static definition table for runtime effects that can contribute final Component/tag results. An `EffectSpec` MUST declare effect id, effect payload id, target binding, duration policy, stack policy, remove policy, payload data, and optional presentation cue id. Formal Luban effect authoring and new tests MUST NOT use `EffectKind` or `RuntimeEffectKind` as the payload entry. Static effect definitions MUST NOT store runtime action ids, target ids, start ticks, expire ticks, or active stack state. Effect payload import SHALL resolve authoring names into registered effect payload ids before authoritative rules consume them.

#### Scenario: EffectSpec 不保存运行时状态
- **WHEN** an effect definition grants temporary pushability for five ticks
- **THEN** the `EffectSpec` declares pushable payload, timed duration policy, and stack policy
- **AND** it does not store the concrete target entity, created tick, expire tick, or runtime effect instance id

#### Scenario: EffectSpec 来自 Luban 独立表
- **WHEN** first-slice effect configuration is loaded
- **THEN** effect definitions are read from Luban-generated `effect_spec` data
- **AND** the main runtime path does not rely on hand-written built-in effect specs as the authoritative configuration source

#### Scenario: EffectSpec payload 受注册表约束
- **WHEN** first-slice effect specs are loaded
- **THEN** their payload ids resolve to registered payload mappers for Blocking, AutoMove, Pushable, PortConnector, MovementPermission, Tag, or future payloads
- **AND** unknown payload ids fail clearly during provider/registry construction
- **AND** adding a future payload does not require adding `EffectKind` or `RuntimeEffectKind` enum members
- **AND** formal Luban source tables do not use effect payload enums for those payloads

### Requirement: Effect Resolver Boundary
系统 SHALL keep final Component/tag resolution owned by `ComponentStateResolver` or an equivalent final-result resolver. Runtime effect data MAY contribute sources, but Rules, ActionArbiter, RulePlanner, and CommitResolver movement validation MUST read only final `GameWorld` Component/tag results when deciding movement, pushability, blocking, or permissions. New effect payload modules SHALL plug into an effect payload mapper registry and final-result resolver registry and MUST NOT require rule modules to inspect runtime effect state or effect-kind enums.

#### Scenario: Runtime effect changes rules through final result
- **WHEN** a runtime effect grants `PushableComponent` to an entity
- **AND** a later push action evaluates that entity
- **THEN** the push rule reads the final `PushableComponent`
- **AND** the rule does not query `RuntimeEffectStore`, `EffectSpec`, `EffectApplication`, `EffectKind`, or `RuntimeEffectKind`

#### Scenario: Effect cannot move entity directly
- **WHEN** an effect payload would imply movement or push behavior
- **THEN** it must create or configure a future action/effect path rather than directly changing PositionComponent
- **AND** actual movement still goes through action, claim, arbitration, planning, and commit

#### Scenario: Resolver output is composable
- **WHEN** static source, runtime effect source, and commit-owned runtime source all contribute the same final result id
- **THEN** final state is computed from all active sources by registered result resolver semantics
- **AND** removing one source removes only that source contribution

#### Scenario: 新 effect payload 不修改 RuntimeEffectKind
- **WHEN** a new effect payload such as stat modifier, shield, mass, or custom movement permission is added
- **THEN** the developer adds an effect payload mapper, payload id registration, config rows, result resolver when needed, and tests
- **AND** the change does not add `EffectKind` or `RuntimeEffectKind` enum members

### Requirement: Effect Application Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for effect application, commit, runtime store lifecycle, resolver final results, stack policy, duration expiry, rule-layer isolation, and id-first effect payload extension. Unity Player build MUST NOT be required.

#### Scenario: Automated validation
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative verification, and Unity EditMode tests for applying, stacking, expiring, and removing first-slice effects
- **AND** tests prove rules read final Component/tag results rather than effect runtime state
- **AND** tests prove adding a test effect payload does not modify `EffectKind`, `RuntimeEffectKind`, or `ComponentStateResolver` result-specific branches

#### Scenario: Registered resolver validation
- **WHEN** Unity TestFramework EditMode tests run for a test-only component result
- **THEN** they register the test result resolver and payload mapper explicitly
- **AND** final component result changes through the same settlement boundary as built-in results

#### Scenario: Manual end-to-end validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** applying and expiring temporary pushable, immobile, auto move, port, or tag effects converges to the same server final state on both clients
- **AND** the client does not locally decide effect hit, active state, expiry, stack behavior, or payload kind
