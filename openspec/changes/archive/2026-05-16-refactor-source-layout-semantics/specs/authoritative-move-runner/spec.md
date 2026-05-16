## ADDED Requirements
### Requirement: Server Authoritative Source Layout Semantics
The Fantasy Hotfix authoritative move source layout SHALL separate network Handler entrypoints, application services, authoritative tick runtime, world bootstrap, synchronization, debug editing, and protocol mapping. Handler files MUST remain thin Fantasy message boundaries and MUST NOT become the place where gameplay rules, storage backend details, or client presentation policy are implemented.

#### Scenario: Handler remains an entrypoint
- **WHEN** a `C2G_*` request handler receives a client request
- **THEN** the handler validates session/request shape, converts protocol data to DG input data, calls an authoritative application service or runtime boundary, and replies
- **AND** it does not directly execute action arbitration, mutate storage adapter internals, construct Arch entities, or build Unity presentation state

#### Scenario: Runtime and sync are discoverable
- **WHEN** a developer needs to inspect server tick, input queue, world delta construction, observer enumeration, or broadcast behavior
- **THEN** those files are located under directories whose names identify runtime or sync responsibilities
- **AND** they are not hidden inside generic infrastructure or Handler folders

#### Scenario: Fantasy conventions survive migration
- **WHEN** server files are moved into the new layout
- **THEN** Fantasy message handlers still use source-generator-compatible classes
- **AND** generated `.g.cs` files are not manually edited
- **AND** async Fantasy code continues to use `FTask` where async behavior is required

### Requirement: Server Layout Migration Verification
The server layout migration SHALL prove that authoritative behavior, session observer behavior, and WorldDelta broadcast behavior remain unchanged by directory and file movement.

#### Scenario: Server verification passes
- **WHEN** the server authoritative verification project runs after migration
- **THEN** player movement, blocked movement, debug spawn, debug move, debug remove, runtime effect debug, observer registration, and WorldDelta sync scenarios still pass
- **AND** failures identify behavior regressions rather than missing file paths

#### Scenario: Manual two-client sync still works
- **WHEN** the user manually starts the server and two Unity clients after migration
- **AND** client A moves, builds, drags, deletes, or applies an approved debug runtime effect
- **THEN** client B observes the server-authoritative final state through snapshot/delta
- **AND** no server behavior depends on the old physical source directory
