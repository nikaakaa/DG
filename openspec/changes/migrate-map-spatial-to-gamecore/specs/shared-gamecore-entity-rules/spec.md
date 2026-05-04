## ADDED Requirements
### Requirement: 共享空间索引
Shared GameCore SHALL provide the authoritative spatial indexing used by both server-side rules and Unity-side shared-rule execution.

#### Scenario: GameWorld uses shared spatial index
- **WHEN** `GameWorld` registers, moves, queries, or removes an entity with `PositionComponent` and `ColliderComponent`
- **THEN** the operation uses the shared GameCore spatial index
- **AND** `GameWorld` does not maintain a separate ad hoc coordinate dictionary for the same entity occupancy

#### Scenario: Movement rule queries shared spatial index
- **WHEN** `MovementResolveSystem` resolves a `MoveCommand`
- **THEN** target-cell entity lookup is answered by the shared GameCore spatial index
- **AND** blocking, occupied, and bouncable behavior remains component-driven

### Requirement: Shared GameCore remains runtime-shell independent
Shared GameCore MUST remain usable by Fantasy server code and Unity client code without depending on either runtime shell.

#### Scenario: No Fantasy dependency
- **WHEN** `Shared/DG.GameCore` is compiled
- **THEN** it does not reference `Fantasy`, `Session`, Fantasy Handler types, or generated protocol message types

#### Scenario: No UnityEngine dependency
- **WHEN** `Shared/DG.GameCore` is compiled
- **THEN** it does not reference `UnityEngine`, `Vector2Int`, `MonoBehaviour`, `GameObject`, or Unity logging APIs

#### Scenario: Unity boundary conversion
- **WHEN** Unity code passes map coordinates into Shared GameCore
- **THEN** Unity code converts between `Vector2Int` and Shared `GridCoord` at the boundary
- **AND** Shared GameCore stores and evaluates rules with `GridCoord`
