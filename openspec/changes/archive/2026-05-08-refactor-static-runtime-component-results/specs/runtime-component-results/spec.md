## ADDED Requirements
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
系统 SHALL provide a runtime effect store that records runtime effect lifecycle state, source identity, target entity, expiry, and stack/source data. The runtime effect store MUST NOT directly mutate `GameWorld` component stores.

#### Scenario: Store records effect without writing world
- **WHEN** a temporary blocking effect is applied to an entity
- **THEN** `RuntimeEffectStore` records the runtime effect instance
- **AND** it does not call `GameWorld.SetComponent`
- **AND** it does not call `GameWorld.RemoveComponent`

#### Scenario: Expired effect requires resolver
- **WHEN** a runtime effect reaches its expire tick
- **THEN** the store marks or removes the runtime effect instance
- **AND** final Component changes occur only after `ComponentStateResolver` resolves active sources

### Requirement: ComponentStateResolver 合成边界
系统 SHALL provide a `ComponentStateResolver` that reads static sources and active runtime sources, computes final Component results, and applies only changed final results to `GameWorld`. The first slice SHALL include `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, and movement permission results; it MUST NOT resolve `PositionComponent`, `DirectionComponent`, `PlayerControlComponent`, or runtime tick counters.

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

### Requirement: GameWorld 保存最终 Component 结果
`GameWorld` SHALL remain the storage for final entity state, component stores, spatial index, dirty tracking, and snapshot/delta data. It MUST NOT become a free-form sink where each static or runtime source writes Component state independently.

#### Scenario: Resolver applies final result
- **WHEN** resolving active sources changes an entity's final `PushableComponent`
- **THEN** `ComponentStateResolver` applies that final result to `GameWorld`
- **AND** `GameWorld` dirty state allows snapshot/delta consumers to observe the final result change

#### Scenario: Sources do not write independently
- **WHEN** a debug tool or future skill creates runtime data
- **THEN** it creates a runtime effect source or world action
- **AND** it does not directly add or remove first-slice components on `GameWorld`

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
系统 SHALL express movable, immovable, rooted, or equivalent movement permission state as final Component results before movement rules execute. Intent arbitration and rule planning MUST decide accepted, rejected, interrupted, or blocked movement from final Component results, not from Ability or RuntimeEffectStore state.

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

#### Scenario: Existing intent tags remain
- **WHEN** `BehaviorIntentDefinitions` resolves existing move, push, auto move, mechanism push, or debug move defaults
- **THEN** existing source, ability, required, blocked, and cancel policy tags may continue to be used
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
