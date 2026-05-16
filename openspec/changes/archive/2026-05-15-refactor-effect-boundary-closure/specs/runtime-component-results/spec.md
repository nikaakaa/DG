## MODIFIED Requirements
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
系统 SHALL provide a `ComponentStateResolver` that reads static sources and active runtime sources, computes final Component results, and applies only changed final results to `GameWorld`. The first slice SHALL include `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, movement permission results, and source-based `WorldTag` results; it MUST NOT resolve `PositionComponent`, `DirectionComponent`, `PlayerControlComponent`, or runtime tick counters.

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

## ADDED Requirements
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
