## ADDED Requirements

### Requirement: Shared Targeting Runtime Boundary
Shared GameCore SHALL own authoritative targeting runtime semantics as pure C# rule code. Targeting runtime code MUST NOT reference UnityEngine, Fantasy, protocol generated types, Unity presentation objects, or client-local mirror-only state.

#### Scenario: Shared targeting has no runtime shell dependency
- **WHEN** `Shared/DG.GameCore` is built after adding targeting runtime support
- **THEN** targeting code does not reference `UnityEngine`, `MonoBehaviour`, `GameObject`, `Session`, Fantasy Handler types, or generated protocol message types
- **AND** server and Unity client mirror code can both understand target data types without owning targeting authority on the client

#### Scenario: client does not decide authoritative hits
- **WHEN** Unity client displays an action result or animation
- **THEN** it consumes server snapshot/delta and metadata
- **AND** it does not locally decide which entities were hit by authoritative targeting

### Requirement: Targeting Uses GameWorld Query Boundary
Targeting selectors SHALL query entities, components, tags, positions, spatial occupancy, and connected-body metadata through stable `GameWorld` or rule-service APIs. They MUST NOT depend on concrete component store internals, dictionary order, storage adapter types, or future ECS implementation details.

#### Scenario: selector spatial query uses stable API
- **WHEN** a target selector scans one or more cells from a source entity
- **THEN** it queries spatial occupancy through `GameWorld` or a stable query service
- **AND** deterministic ordering is applied explicitly after query

#### Scenario: selector classes remain behind targeting boundary
- **WHEN** a new target algorithm is added as a selector class or extension
- **THEN** rule modules access it through the targeting registry
- **AND** server Hotfix code and Unity client mirror code do not instantiate selector implementation classes directly

#### Scenario: storage migration preserves targeting semantics
- **WHEN** `GameWorld` internal storage changes from dictionary-backed storage to indexed or ECS-like storage
- **THEN** targeting output for the same authoritative world state remains semantically equivalent
- **AND** tests assert target data and final rule results rather than internal storage layout
