## ADDED Requirements

### Requirement: Arch ECS Backend Is Internal GameWorld Storage
Shared GameCore SHALL provide an Arch-backed implementation of the authoritative `IWorldDataStorage` boundary. Arch MUST be an internal storage backend of `GameWorld`; rules, protocol conversion, Fantasy handlers, and client mirror code MUST continue to use DG `GameWorld`, DG components, DG `EntityId`, and snapshot/delta contracts.

#### Scenario: Rules do not see Arch types
- **WHEN** action arbitration, rule planning, conflict resolution, commit, runtime effect resolution, or delta construction reads authoritative world state
- **THEN** it calls `GameWorld` public APIs or DG component APIs
- **AND** it does not store or accept `Arch.World`, `Arch.Entity`, Arch query descriptions, Arch chunks, or Arch row handles

#### Scenario: Arch backend can replace indexed storage
- **WHEN** a `GameWorld` is created for service-authoritative simulation
- **THEN** it can be backed by Arch storage through the `IWorldDataStorage` boundary
- **AND** external callers still use the same `GameWorld` APIs as the indexed storage backend

#### Scenario: Backend swap preserves rule contract
- **WHEN** the same entities, components, actions, and server ticks are run against indexed storage and Arch storage
- **THEN** final DG snapshots and deltas are equivalent
- **AND** no rule result depends on an Arch internal entity id

### Requirement: DG EntityId Remains The Only External Identity
The system SHALL keep DG `EntityId` as the only identity visible outside storage. Arch entity handles MAY exist inside `ArchWorldDataStorage`, but MUST NOT appear in protocol payloads, snapshots, deltas, logs that define behavior contracts, session ownership, config ids, or client mirror state.

#### Scenario: Create entity maps identities
- **WHEN** `GameWorld.AddEntity` creates an entity in Arch-backed storage
- **THEN** storage creates an Arch entity
- **AND** storage records a stable mapping between DG `EntityId` and the Arch entity
- **AND** `GameWorld.TryGetEntity` returns the DG entity by DG `EntityId`

#### Scenario: Remove entity clears identities
- **WHEN** `GameWorld.RemoveEntity` removes an entity from Arch-backed storage
- **THEN** storage destroys or removes the matching Arch entity
- **AND** storage removes both identity mapping directions
- **AND** later reads by the old DG `EntityId` fail instead of reading a recycled Arch entity

#### Scenario: Snapshot and delta use DG ids
- **WHEN** `GameWorld.CreateSnapshot` or `GameWorld.FlushDelta` emits authoritative state
- **THEN** all entity identities in the result are DG `EntityId` values
- **AND** no Arch entity id or row id is serialized

### Requirement: Arch Backend Supports Authoritative Components And Queries
Arch-backed storage SHALL support the authoritative component set and component-combination queries required by current server gameplay hot paths.

#### Scenario: Component access works through GameWorld
- **WHEN** `GameWorld.SetComponent`, `TryGetComponent`, `HasComponent`, `GetComponent`, or `RemoveComponent` is called for an authoritative component
- **THEN** the operation reads or writes the Arch-backed component storage
- **AND** the caller does not need Arch-specific APIs

#### Scenario: Auto move query uses Arch backend
- **WHEN** service-authoritative tick queries entities with `PositionComponent`, `DirectionComponent`, and `AutoMoveComponent`
- **THEN** Arch-backed storage returns the same DG entity ids and component values as indexed storage for the same world state
- **AND** the query result is exposed through DG `QueryAutoMove`

#### Scenario: Push-on-enter query uses Arch backend
- **WHEN** service-authoritative tick queries entities with `PositionComponent`, `DirectionComponent`, and `PushOnEnterComponent`
- **THEN** Arch-backed storage returns the same DG entity ids and component values as indexed storage for the same world state
- **AND** the query result is exposed through DG `QueryPushOnEnter`

#### Scenario: Spatial hot paths remain DG controlled
- **WHEN** rules query blocking, collider, pushable, port, tag, or movement permission data
- **THEN** component values may come from Arch-backed storage
- **AND** grid occupancy and target-layer filtering remain controlled by DG spatial APIs

### Requirement: Spatial Dirty Snapshot And Runtime Effects Stay In DG
Arch backend SHALL NOT replace DG spatial indexing, dirty journaling, runtime effect final component resolution, snapshot generation, delta generation, or animation metadata ownership.

#### Scenario: Position update synchronizes DG spatial state
- **WHEN** `GameWorld.SetComponent` changes `PositionComponent` in Arch-backed storage
- **THEN** DG spatial location and occupancy are updated by `GameWorld`
- **AND** DG dirty state records the changed entity

#### Scenario: Runtime effect resolves final component
- **WHEN** runtime effect state changes the final component value for an entity
- **THEN** the final component is written through `GameWorld` component APIs
- **AND** Arch storage stores the component value
- **AND** DG dirty/delta semantics remain unchanged

#### Scenario: Delta generation remains storage-layout independent
- **WHEN** `GameWorld.FlushDelta` runs after Arch-backed component changes
- **THEN** changed entity snapshots, removed ids, and animation metadata are produced by DG dirty journal and snapshot logic
- **AND** the delta does not expose Arch storage layout
