## ADDED Requirements
### Requirement: EffectSpec 静态定义
系统 SHALL define `EffectSpec` as a Luban-backed static definition table for runtime effects that can contribute final Component/tag results. An `EffectSpec` MUST declare effect id, effect kind, target binding, duration policy, stack policy, remove policy, payload kind, and optional presentation cue id. Static effect definitions MUST NOT store runtime action ids, target ids, start ticks, expire ticks, or active stack state.

#### Scenario: EffectSpec 不保存运行时状态
- **WHEN** an effect definition grants temporary pushability for five ticks
- **THEN** the `EffectSpec` declares pushable payload, timed duration policy, and stack policy
- **AND** it does not store the concrete target entity, created tick, expire tick, or runtime effect instance id

#### Scenario: EffectSpec 来自 Luban 独立表
- **WHEN** first-slice effect configuration is loaded
- **THEN** effect definitions are read from Luban-generated `effect_spec` data
- **AND** the main runtime path does not rely on hand-written built-in effect specs as the authoritative configuration source

#### Scenario: EffectSpec payload 受限于 first slice
- **WHEN** first-slice effect specs are loaded
- **THEN** they may describe Blocking, AutoMove, Pushable, PortConnector, MovementPermission, or Tag result payloads
- **AND** they do not describe Position, Direction, PlayerControl, health, mass, or other Attribute/Stat mutation yet
- **AND** Tag result payloads use existing `WorldTag`

### Requirement: EffectApplication 运行时实例输入
系统 SHALL create `EffectApplication` values from action execution output before runtime effect state is written. Each `EffectApplication` MUST carry source action context or equivalent source context, effect spec id, resolved target data, target entity/body/cell binding, start tick, expire tick or infinite duration marker, stack key, causality id, and resolved payload values.

#### Scenario: Action 命中目标生成 EffectApplication
- **WHEN** an action with `ApplyRuntimeEffect` execution hits two target entities
- **THEN** execution produces one `EffectApplication` per resolved target
- **AND** each application records the source action id, effect spec id, target entity id, start tick, expire tick, stack key, and causality id

#### Scenario: EffectApplication 不直接写世界
- **WHEN** execution creates an `EffectApplication`
- **THEN** no final Component, tag, position, direction, or entity lifecycle state is written directly
- **AND** the application must be accepted through commit before it can affect runtime effect state

### Requirement: Effect Commit Boundary
系统 SHALL apply runtime effects through commit proposals. `AddRuntimeEffect`, `RemoveRuntimeEffect`, `SetComponentResult`, `AddTag`, and `RemoveTag` proposals MUST be resolved by the commit layer in deterministic order. Strategy, policy, and effect application code MUST NOT directly mutate `GameWorld` final Component/tag stores. `SetComponentResult` MUST represent a runtime component contribution/source in the first slice and MUST NOT override the final component result directly.

#### Scenario: AddRuntimeEffect writes store through commit
- **WHEN** an accepted effect application grants temporary immobile state
- **THEN** action execution emits an `AddRuntimeEffect` commit proposal
- **AND** `CommitResolver` writes the runtime effect source into `RuntimeEffectStore`
- **AND** final `MovementPermissionComponent` changes only after `ComponentStateResolver` resolves active sources

#### Scenario: RemoveRuntimeEffect removes source through commit
- **WHEN** a runtime effect is removed by explicit effect id or stack key
- **THEN** execution emits a `RemoveRuntimeEffect` commit proposal
- **AND** `CommitResolver` removes the matching runtime effect source
- **AND** final Component/tag state is recomputed by the resolver

#### Scenario: Remove one source keeps other sources
- **WHEN** an entity has static Pushable contribution
- **AND** two active runtime effects also contribute Pushable
- **AND** one runtime effect is removed
- **THEN** only that effect application's source is removed
- **AND** the final Pushable result remains active because other sources still contribute it

#### Scenario: Commit 不解释玩法名
- **WHEN** two different effect ids both grant temporary pushability with the same payload
- **THEN** commit applies them through the same proposal kind and payload data
- **AND** commit does not branch on readable effect name or action name

### Requirement: Effect Contribution Settlement
系统 SHALL preserve effect component/tag contributions as independent sources and compute final results through `ComponentStateResolver` or an equivalent settlement layer. Adding a buff/effect MUST add its own contribution source, removing a buff/effect MUST remove only its own contribution source, and final component/tag state MUST be recalculated from the remaining sources.

#### Scenario: Add and remove order is consistent
- **WHEN** effect A and effect B both grant temporary blocking to the same entity
- **AND** effect A is removed before effect B
- **THEN** only effect A's contribution source is removed
- **AND** final Blocking remains active until effect B is also removed or expired

#### Scenario: Static source survives runtime source removal
- **WHEN** an entity has static Blocking from its archetype
- **AND** a runtime effect also grants Blocking
- **AND** the runtime effect expires
- **THEN** the final Blocking result remains active because the static source still contributes it

#### Scenario: Settlement owns final state
- **WHEN** active effect contributions change during a tick
- **THEN** the settlement/resolver recomputes final component/tag result from active sources
- **AND** no effect application writes the final component/tag result by itself

### Requirement: Effect Duration And Expiry
系统 SHALL support instant, timed tick, and infinite-until-remove duration policies for the first effect layer slice. Timed effects MUST expire by server tick, and expiry MUST remove only the runtime source contributed by that effect instance.

#### Scenario: Timed effect expires
- **WHEN** a temporary pushable effect starts at tick 10 with duration 5
- **THEN** it is active for rule-visible resolution before tick 15
- **AND** it is expired and removed as a runtime source at tick 15 or later according to the authoritative tick order
- **AND** final pushability remains only if another static or runtime source still contributes it

#### Scenario: Infinite effect waits for explicit remove
- **WHEN** an infinite runtime port effect is applied
- **THEN** it remains active across ticks
- **AND** it is removed only by explicit remove policy or entity removal

### Requirement: Effect Stack Policy
系统 SHALL resolve effect stack policy deterministically before adding or refreshing runtime effect instances. The first slice MUST support replace by stack key, refresh duration, allow multiple, and reject duplicate behavior. Stack policy MUST NOT depend on dictionary order, client arrival order, or readable effect names.

#### Scenario: Refresh duration keeps one source
- **WHEN** the same source applies the same timed effect with the same stack key twice
- **AND** the effect stack policy is refresh duration
- **THEN** the runtime store contains one effective runtime source for that stack key
- **AND** its expire tick is refreshed deterministically

#### Scenario: Allow multiple preserves independent sources
- **WHEN** two sources apply the same effect to one target
- **AND** the effect stack policy is allow multiple
- **THEN** both runtime sources remain active independently
- **AND** removing one source does not remove the other source's final contribution

### Requirement: Effect Resolver Boundary
系统 SHALL keep final Component/tag resolution owned by `ComponentStateResolver` or an equivalent final-result resolver. Runtime effect data MAY contribute sources, but Rules, ActionArbiter, RulePlanner, and CommitResolver movement validation MUST read only final `GameWorld` Component/tag results when deciding movement, pushability, blocking, or permissions.

#### Scenario: Runtime effect changes rules through final result
- **WHEN** a runtime effect grants `PushableComponent` to an entity
- **AND** a later push action evaluates that entity
- **THEN** the push rule reads the final `PushableComponent`
- **AND** the rule does not query `RuntimeEffectStore`, `EffectSpec`, `EffectApplication`, or `EffectKind`

#### Scenario: Effect cannot move entity directly
- **WHEN** an effect payload would imply movement or push behavior
- **THEN** it must create or configure a future action/effect path rather than directly changing PositionComponent
- **AND** actual movement still goes through action, claim, arbitration, planning, and commit

### Requirement: Effect Application Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for effect application, commit, runtime store lifecycle, resolver final results, stack policy, duration expiry, and rule-layer isolation. Unity Player build MUST NOT be required.

#### Scenario: Automated validation
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative verification, and Unity EditMode tests for applying, stacking, expiring, and removing first-slice effects
- **AND** tests prove rules read final Component/tag results rather than effect runtime state

#### Scenario: Manual end-to-end validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** applying and expiring temporary pushable, immobile, auto move, port, or tag effects converges to the same server final state on both clients
- **AND** the client does not locally decide effect hit, active state, expiry, or stack behavior
