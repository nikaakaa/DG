## ADDED Requirements
### Requirement: Unity ClientWorld Source Layout Semantics
Unity ClientWorld source files SHALL be organized by client responsibility: bootstrap, networking, mirror state, presentation, input, debug tools, editor-only tools, and tests. The layout MUST make it clear that default Play Mode client code consumes server-authoritative snapshot/delta results rather than deciding authoritative gameplay rules locally.

#### Scenario: Mirror and presentation are separate
- **WHEN** a developer inspects client world state synchronization
- **THEN** mirror-state code that applies snapshots, deltas, entity ids, components, and dirty state is located under a mirror responsibility
- **AND** visual animation, GameObject presentation, ghost previews, and UI feedback are located under presentation or debug tool responsibilities

#### Scenario: Networking does not hide local authority
- **WHEN** a client networking handler receives `G2C_WorldSnapshotNotify`, `G2C_WorldDeltaNotify`, or movement result data
- **THEN** it routes server final state into the mirror boundary
- **AND** it does not run local movement, push, port merge, blocked result, targeting, or runtime effect authority rules

#### Scenario: Unity references survive file movement
- **WHEN** Unity refreshes scripts after ClientWorld files are moved
- **THEN** `.cs` and `.meta` pairs preserve serialized references for scenes, prefabs, and editor assets where those references exist
- **AND** ClientWorld scripts compile without requiring a Unity Player build

### Requirement: Unity ClientWorld Layout Verification
Unity ClientWorld layout migration SHALL include Unity TestFramework EditMode coverage and a manual Play Mode end-to-end validation path.

#### Scenario: EditMode tests cover migrated boundaries
- **WHEN** Unity TestFramework EditMode tests run after migration
- **THEN** tests cover mirror snapshot/delta application, client animation metadata consumption, debug layout tooling, port visualization source data, and server-authoritative submitter failure behavior
- **AND** tests prove debug tools and networking submitters do not directly mutate authoritative mirror state before server sync

#### Scenario: Manual end-to-end validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **AND** client A performs movement, debug spawn, debug drag, debug delete, and a runtime effect debug action if available
- **THEN** both clients converge to the same server final state
- **AND** the user can verify that visual presentation follows mirror state from server snapshot/delta
