# runtime-component-results Specification

## Purpose
TBD - created by archiving change refactor-static-runtime-component-results. Update Purpose after archive.
## Requirements
### Requirement: Component 结果层唯一规则语言
系统 SHALL 将 Component 结果层定义为 System / Rules 可读取的最终状态语言。静态配置、运行时 effect 和 debug runtime state MUST stay before the Component result layer, and System / Rules MUST NOT query Ability or Effect state to decide world rules.

#### Scenario: Rules read final Component
- **WHEN** auto move, blocking, push, port connection, or movement permission rules evaluate an entity
- **THEN** the rules read final components such as `AutoMoveComponent`, `BlockingComponent`, `PushableComponent`, `PortConnectorComponent`, and movement permission results
- **AND** the rules do not query `RuntimeEffectStore`, `AbilityKind`, or `EffectKind`

#### Scenario: Runtime source is not rule input
- **WHEN** a temporary runtime effect contributes `AutoMoveComponent`
- **THEN** the effect is resolved into a final `AutoMoveComponent` before auto move rules run
- **AND** auto move rules do not need to know which effect created the final result

### Requirement: 静态与运行时来源分离
系统 SHALL keep static data and runtime data as separate sources before final Component results. Static archetype sources and runtime effect sources MAY both contribute the same Component result, but removing a runtime source MUST NOT remove a result that is still contributed by static data or another runtime source.

#### Scenario: Runtime remove keeps static Blocking
- **WHEN** an entity has static `BlockingComponent` from its archetype
- **AND** a runtime effect also contributes `BlockingComponent`
- **AND** that runtime effect is removed
- **THEN** the entity still has final `BlockingComponent`

#### Scenario: Last runtime source removes AutoMove
- **WHEN** an entity has no static `AutoMoveComponent`
- **AND** runtime effect A and runtime effect B both contribute `AutoMoveComponent`
- **AND** runtime effect A is removed
- **THEN** the entity still has final `AutoMoveComponent`
- **WHEN** runtime effect B is removed
- **THEN** the final `AutoMoveComponent` is removed

### Requirement: RuntimeEffectStore 生命周期边界
系统 SHALL provide a runtime effect store that records runtime effect lifecycle state, source identity, target entity, expiry, and stack/source data. The runtime effect store MUST NOT directly mutate `GameWorld` component stores. Runtime effect store mutation MUST be reachable from ordinary production paths only through accepted commit proposals or equivalent commit-owned services; public `GameWorld` APIs MUST NOT allow ordinary action, client, debug, or test code to bypass commit for runtime effect add/remove.

#### Scenario: Store records effect without writing world
- **WHEN** a temporary blocking effect is applied to an entity
- **THEN** `RuntimeEffectStore` records the runtime effect instance
- **AND** it does not call `GameWorld.SetComponent`
- **AND** it does not call `GameWorld.RemoveComponent`

#### Scenario: Expired effect requires resolver
- **WHEN** a runtime effect reaches its expire tick
- **THEN** the store marks or removes the runtime effect instance
- **AND** final Component changes occur only after `ComponentStateResolver` resolves active sources

#### Scenario: 普通路径不能绕过 commit 写 effect
- **WHEN** action strategy、server debug service、Unity client submitter 或普通测试需要添加或移除 runtime effect
- **THEN** 它们必须提交 `AddRuntimeEffect`、`RemoveRuntimeEffect` 或等价 commit-owned request
- **AND** 它们不能直接调用 public `GameWorld.AddRuntimeEffect` 或 public `GameWorld.RemoveRuntimeEffect`

#### Scenario: 测试入口也走 commit 边界
- **WHEN** automated tests need to create runtime effect state
- **THEN** tests use commit proposals, commit test helpers, or explicitly internal fixtures
- **AND** tests do not normalize bypassing commit as the documented effect application path

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

### Requirement: GameWorld 保存最终 Component 结果
`GameWorld` SHALL remain the storage for final entity state, component stores, spatial index, dirty tracking, and snapshot/delta data. It MUST NOT become a free-form sink where each static or runtime source writes Component or tag state independently. First-slice component/tag final results MUST be applied by `ComponentStateResolver` or commit-owned final-result settlement, not by source-specific direct writes.

#### Scenario: Resolver applies final result
- **WHEN** resolving active sources changes an entity's final `PushableComponent`
- **THEN** `ComponentStateResolver` applies that final result to `GameWorld`
- **AND** `GameWorld` dirty state allows snapshot/delta consumers to observe the final result change

#### Scenario: Sources do not write independently
- **WHEN** a debug tool or future skill creates runtime data
- **THEN** it creates a runtime effect source or world action
- **AND** it does not directly add or remove first-slice components on `GameWorld`

#### Scenario: Runtime tag remove does not edit final tag directly
- **WHEN** a runtime tag effect or runtime tag proposal is removed
- **THEN** commit removes the matching tag source
- **AND** final `TagSetComponent` changes only after resolver recomputes active tag sources

### Requirement: Ability 禁止边界
系统 SHALL NOT introduce Ability as a solution for Component lifecycle, runtime effect lifecycle, dynamic ports, or movement arbitration in the first runtime-component-results slice. AbilityKind MUST NOT map to ComponentKind, and ability grant/remove MUST NOT mutate final Component results.

#### Scenario: Ability module is not introduced
- **WHEN** the first slice is implemented
- **THEN** no new Ability module is required to apply runtime effects
- **AND** dynamic port and movement permission behavior are implemented through runtime effects, resolver results, and Rules arbitration

#### Scenario: Ability mapping is forbidden
- **WHEN** code or tests inspect the first slice
- **THEN** there is no `AbilityKind -> ComponentKind` mapping
- **AND** ability grant/remove does not write `GameWorld` components

### Requirement: Dynamic Port 结果合成
系统 SHALL resolve static and runtime port sources into final `PortConnectorComponent` results before port connection rules execute. `PortConnectionSystem` MUST read final `PortConnectorComponent` state and MUST NOT query runtime effect data.

#### Scenario: Static and runtime ports merge
- **WHEN** an entity has a static left port
- **AND** an active runtime effect contributes a right port
- **THEN** the final `PortConnectorComponent` contains left and right ports
- **AND** `PortConnectionSystem` reads that final component result

#### Scenario: Runtime port removal preserves static port
- **WHEN** an entity has a static left port
- **AND** an active runtime effect contributes a right port
- **AND** the runtime effect is removed
- **THEN** the final `PortConnectorComponent` still contains the static left port
- **AND** the final right port is removed when no other source contributes it

### Requirement: Movement Permission 仲裁边界
系统 SHALL express movable, immovable, rooted, or equivalent movement permission state as final Component results before movement rules execute. Action arbitration and rule planning MUST decide accepted, rejected, interrupted, or blocked movement from final Component results, not from Ability or RuntimeEffectStore state.

#### Scenario: Runtime immobile rejects movement
- **WHEN** an entity has an active runtime effect that contributes an immobile or rooted final result
- **AND** a player move action is processed for that entity
- **THEN** the Rules layer rejects or interrupts the movement based on the final result
- **AND** the RuntimeEffectStore does not decide the movement result directly

#### Scenario: Pushability uses final result
- **WHEN** a runtime effect adds or removes pushability for an entity
- **THEN** the final `PushableComponent` or equivalent result changes through `ComponentStateResolver`
- **AND** push rules read that final result when deciding whether the entity can be pushed

### Requirement: WorldTag 迁移遗留边界
系统 MAY keep the existing `WorldTag` arbitration language during this slice, but new runtime component result work MUST NOT expand System / Rules dependencies on tags as a substitute for final Component results.

#### Scenario: Existing action tags remain
- **WHEN** `ActionSpec` resolves existing move, push, auto move, mechanism push, or debug move defaults
- **THEN** existing source, ability, required, blocked, and interrupt policy tags may continue to be used
- **AND** runtime effect source identity is not expressed by adding new rule-specific tag dependencies

#### Scenario: New state uses component result
- **WHEN** a runtime effect contributes temporary blocking, pushability, port, or movement permission state
- **THEN** the resulting rule-visible state is expressed as final Component result
- **AND** the rule does not require a new `WorldTag` to discover the runtime effect

### Requirement: Runtime Component Result 验证
系统 SHALL include Unity TestFramework EditMode coverage for the first slice before it is treated as complete. Manual Play Mode verification SHALL be used for end-to-end service/client confirmation, and Unity Player build MUST NOT be required.

#### Scenario: EditMode verifies source merge
- **WHEN** Unity TestFramework EditMode tests run
- **THEN** they verify static + runtime source merge
- **AND** they verify runtime source removal does not remove surviving static or runtime contributors

#### Scenario: Manual verification proves client mirror
- **WHEN** a developer manually runs server and two Unity Play Mode clients
- **THEN** the observer client sees server-synchronized final Component results
- **AND** the client does not locally resolve runtime effects

### Requirement: Runtime Source Reset Boundary
系统 SHALL provide a commit-owned way to remove all runtime effect and runtime contribution sources for a target entity while preserving static archetype sources and non-effect-owned final state. This reset MUST restore first-slice final Component/tag results to the state implied by remaining static sources and surviving unrelated runtime sources.

#### Scenario: Reset removes runtime sources only
- **WHEN** an entity has static Blocking and runtime Pushable, runtime PortConnector, and runtime tag sources
- **AND** reset runtime sources is committed for that entity
- **THEN** runtime Pushable, runtime PortConnector, and runtime tag sources for that entity are removed
- **AND** static Blocking remains in the final result

#### Scenario: Reset does not rollback movement
- **WHEN** an entity moved through normal action commit after receiving runtime effects
- **AND** reset runtime sources is committed for that entity
- **THEN** PositionComponent and DirectionComponent are not restored to spawn values by this reset
- **AND** only first-slice runtime component/tag contributions are recalculated

#### Scenario: Add remove order returns to static result
- **WHEN** runtime effects A、B、C are added to one entity in one order
- **AND** they are removed in a different order or cleared by runtime reset
- **THEN** final first-slice Component/tag results equal the static source result
- **AND** no removed runtime source continues to affect resolver output

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

