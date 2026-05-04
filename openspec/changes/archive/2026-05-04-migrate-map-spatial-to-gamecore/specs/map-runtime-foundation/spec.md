## ADDED Requirements
### Requirement: Shared Spatial Runtime Location
Map spatial runtime logic that is required by both authoritative server rules and Unity client prediction SHALL live in `Shared/DG.GameCore` as pure C#.

#### Scenario: Shared coordinate service
- **WHEN** server or Unity client code needs chunk key, cell key, chunk coordinate, local coordinate, or world coordinate conversion
- **THEN** the shared coordinate service in `DG.GameCore` provides the calculation
- **AND** new rule-layer spatial key logic is not duplicated in Unity-only code

#### Scenario: Shared chunk storage
- **WHEN** server or Unity client shared-rule code requests chunks or cells
- **THEN** shared chunk storage provides chunk and cell access without requiring Unity runtime types

#### Scenario: Unity editor tools stay outside Shared
- **WHEN** Unity editor windows, gizmos, visual debugging, or scene rendering need to inspect chunks and cells
- **THEN** those tools remain in Unity client code
- **AND** they consume Shared data through adapters instead of becoming part of Shared GameCore

### Requirement: Shared Target Spatial Query
Shared spatial runtime SHALL support target-partitioned entity queries without depending on Unity `Entity` objects.

#### Scenario: Register entity target
- **WHEN** an entity with a target/category is registered into the shared spatial index
- **THEN** the entity is indexed in the all-target view
- **AND** the entity is indexed in its specific target/category view

#### Scenario: Query chunk by target
- **WHEN** caller queries a chunk by target/category
- **THEN** the shared spatial index returns only entity ids or shared entity references matching that target/category
- **AND** the query does not require scanning unrelated targets first

#### Scenario: Move entity between chunks
- **WHEN** an indexed entity moves from one coordinate to another
- **THEN** old cell/chunk indexes are updated
- **AND** new cell/chunk indexes are updated
- **AND** target/category partitioning is preserved
