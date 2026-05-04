## ADDED Requirements
### Requirement: Unity Client GameCore Reference
Unity client SHALL be able to compile and execute the shared `DG.GameCore` rule layer for tests and later prediction.

#### Scenario: Unity references GameCore package
- **WHEN** Unity client code references `DG.GameCore`
- **THEN** the client project compiles through a local package reference to Shared GameCore
- **AND** the client does not copy Shared source into a second divergent implementation
- **AND** Shared GameCore remains independent of Unity runtime APIs

#### Scenario: Unity test executes shared movement rule
- **WHEN** a Unity TestFramework EditMode test creates a `GameWorld` with a movable entity and a blocking entity
- **THEN** the test can execute `MovementResolveSystem`
- **AND** the result matches the same component-driven blocking behavior used by the server

### Requirement: Client World Rule Truth Boundary
Unity client SHALL treat Shared GameCore state as the rule truth for shared-rule execution, and Unity-specific world objects MUST NOT retain a separate rule implementation.

#### Scenario: Snapshot updates shared mirror state
- **WHEN** Unity client receives a server world snapshot or delta
- **THEN** the client can apply the authoritative entity state into a Shared GameCore mirror world
- **AND** Unity visual state is updated from that mirror

#### Scenario: Unity-only world is removed from rule decisions
- **WHEN** an interaction depends on movement, blocking, bouncing, or spatial occupancy rules
- **THEN** the rule decision is made by Shared GameCore when running shared-rule mode
- **AND** Unity-only `World`, `EntityTracker`, or `DirtyTracker` does not remain as a parallel rule implementation

#### Scenario: Prediction remains out of scope
- **WHEN** this migration is implemented
- **THEN** the client is not required to perform prediction, rollback, or reconciliation
- **AND** the migration only establishes the shared rule/runtime foundation required by later prediction work
