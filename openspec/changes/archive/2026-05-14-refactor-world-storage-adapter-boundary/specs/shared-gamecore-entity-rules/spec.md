## MODIFIED Requirements

### Requirement: ECS Storage Abstraction Boundary
Shared GameCore SHALL expose world, entity, component, query, spatial, dirty, snapshot, and delta semantics through stable APIs that do not reveal whether component data is stored in dictionaries, arrays, indexed pools, third-party ECS storage, or archetype chunks. `GameWorld` SHALL own the only public access boundary for rule, runtime effect, debug edit, server sync, and client mirror code. External rule modules MUST NOT depend on dictionary key order, per-query array allocation, concrete `ComponentStore<T>` internals, concrete indexed storage internals, storage adapter types, or third-party ECS types.

#### Scenario: 外部系统不依赖字典顺序
- **WHEN** a rule module needs to enumerate entities
- **THEN** it uses an API whose ordering contract is explicit
- **AND** unordered iteration is not treated as entity id sorted order
- **AND** sorted iteration is requested only when deterministic output requires it

#### Scenario: 查询语义不依赖内部存储
- **WHEN** a rule module queries entities with position, collider, auto move, push-on-enter, or port connector data
- **THEN** it uses a `GameWorld` query or subset boundary
- **AND** the result is valid whether the internal storage is dictionary-backed, indexed-pool-backed, third-party-ECS-backed, or archetype-backed
- **AND** tests assert behavior and world state rather than internal chunk, row, pool, mask, query cache, or dictionary layout

#### Scenario: snapshot/delta 语义保持
- **WHEN** GameCore changes its internal component storage implementation
- **THEN** `EntitySnapshot`, `WorldDelta`, dirty entity ids, changed cells, changed chunks, animation metadata, and removed entity ids preserve their existing external semantics
- **AND** server-authoritative sync consumers do not need to know the internal storage layout

#### Scenario: storage adapter 只在 GameWorld 内部
- **WHEN** Shared GameCore introduces an `IWorldDataStorage` or equivalent storage adapter contract
- **THEN** `GameWorld` owns that adapter
- **AND** rule modules, server Hotfix code, Unity client mirror code, protocol handlers, planner, commit resolver, arbiter, and runtime effect consumers do not directly reference the adapter
- **AND** the adapter does not become a replacement public API for `GameWorld`

#### Scenario: indexed adapter 保持当前语义
- **WHEN** the current storage implementation is expressed as an indexed or dense-pool adapter
- **THEN** entity id lookup, component add/update/remove, component query, entity enumeration order, and entity remove/recreate semantics remain equivalent to the existing `GameWorld` behavior
- **AND** old entity ids do not read newly reused internal rows

#### Scenario: Arch 不能污染规则层
- **WHEN** a future proposal evaluates Arch or another third-party ECS as an internal storage adapter
- **THEN** third-party ECS types remain behind the `GameWorld` storage adapter boundary
- **AND** `ActionArbiter`, `RulePlanning`, `CommitRules`, `BodyCapabilityResolver`, `StateDrivenRules`, `AuthoritativeWorldTickRunner`, `AuthoritativeWorldSyncSystem`, and `ClientMapWorld` do not reference third-party ECS types
- **AND** `WorldDelta` remains constructed from DG-owned dirty and snapshot semantics

#### Scenario: 本变更不引入第三方 ECS
- **WHEN** this storage adapter boundary change is implemented
- **THEN** Shared GameCore does not add Arch, Unity DOTS, DefaultEcs, Flecs, or another third-party ECS dependency
- **AND** any third-party ECS evaluation requires a separate OpenSpec proposal

#### Scenario: storage adapter 不拥有 DG 权威语义
- **WHEN** `GameWorld` delegates entity, component, or component-query operations to a storage adapter
- **THEN** `GameWorld` still owns spatial occupancy synchronization, dirty journal updates, changed cell/chunk tracking, runtime effect final component resolution, snapshot creation, and delta flushing
- **AND** the adapter does not decide push, blocked result, connected body, action arbitration, Fantasy session ownership, or client mirror convergence
