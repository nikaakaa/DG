## ADDED Requirements

### Requirement: Fantasy Server Uses Arch-Backed Authoritative GameWorld
The Fantasy server SHALL create and tick one service-authoritative `GameWorld` backed by Arch storage while preserving Fantasy as the network and lifecycle shell. Fantasy handlers and tick runner SHALL not directly access Arch APIs.

#### Scenario: World provider creates Arch-backed world
- **WHEN** the authoritative move world provider initializes the service world
- **THEN** it creates a `GameWorld` whose storage backend is Arch-backed
- **AND** it keeps `GameWorld` as the only world object passed to tick runner, sync system, debug services, and entity manager

#### Scenario: Handler submits input without Arch access
- **WHEN** `C2G_MoveRequest`, debug edit requests, JoinWorld, or observer registration are handled
- **THEN** handlers validate session and submit intent through existing authoritative services
- **AND** handlers do not create Arch entities, query Arch worlds, or set Arch components directly

#### Scenario: Tick runner remains GameWorld based
- **WHEN** `AuthoritativeWorldTickRunner` advances a server tick
- **THEN** it drains input, enqueues actions, runs rule execution, records metadata, and broadcasts deltas through `GameWorld`
- **AND** it does not depend on Arch storage handles

### Requirement: Unified Authoritative Tick And Speed Model
The service-authoritative runtime SHALL use server tick as the single settlement cadence. Player movement, auto push source output, mechanism push, connected body movement, and debug-authoritative changes MUST resolve through server tick and DG world delta semantics. Logical movement speed SHALL be defined by action cost, ready tick, and authoritative commit, not by client frame rate.

#### Scenario: Player movement speed is server-authoritative
- **WHEN** a player movement request is accepted
- **THEN** the request becomes a DG action with a configured cost or ready tick
- **AND** the final coordinate changes only when the server tick commits that action
- **AND** client frame rate does not decide the final coordinate

#### Scenario: Auto push source cadence is configured server-side
- **WHEN** an entity with `AutoMoveComponent` is evaluated
- **THEN** service-authoritative tick uses the component and ActionSpec/cost policy to decide whether a configured push/action output is ready
- **AND** that output enters the same action arbitration and commit path as other push-like behavior
- **AND** the resulting WorldDelta records the authoritative server tick for any committed state change

#### Scenario: Mechanism push speed follows action policy
- **WHEN** push-on-enter or another mechanism emits a movement action
- **THEN** the movement uses configured action cost / ready tick / commit policy
- **AND** it does not bypass action arbitration by directly changing coordinates

#### Scenario: Connected body members share authoritative tick
- **WHEN** connected body movement commits successfully
- **THEN** every moved member snapshot in the WorldDelta uses the same authoritative server tick
- **AND** animation metadata identifies the same tick for client playback

### Requirement: Parallel Candidate Compute Before Deterministic Commit
The service-authoritative runtime SHALL separate large read-only scans from authoritative mutation. Parallel work MAY collect candidates from Arch-backed component queries and DG readonly spatial views, but all world mutation, action enqueue finalization, arbitration, component writes, dirty writes, result completion, delta construction, and Fantasy broadcast MUST remain in a deterministic commit phase.

#### Scenario: Auto push source candidates are collected without mutation
- **WHEN** service tick scans auto source entities in parallel
- **THEN** workers may output candidate push/action outputs containing DG entity ids, direction, configured output action spec, cost, and ready tick data
- **AND** workers must not write `GameWorld`, dirty state, move results, or network messages

#### Scenario: Push-on-enter candidates are collected without mutation
- **WHEN** service tick scans push-on-enter sources in parallel
- **THEN** workers may output candidate mechanism actions
- **AND** final action queue insertion and conflict ordering happen only in the deterministic commit phase

#### Scenario: Parallel and serial candidate modes are equivalent
- **WHEN** the same world state and input are processed with serial candidate scan and parallel candidate scan
- **THEN** the candidate set after stable sorting is equivalent
- **AND** committed WorldDelta results are equivalent

#### Scenario: Commit order is stable
- **WHEN** multiple candidates are collected in the same server tick
- **THEN** commit phase orders them by stable DG keys such as server tick, ready tick, priority, source entity id, and action sequence
- **AND** thread scheduling order does not affect authoritative results

### Requirement: Arch Backend Does Not Change Network Authority
Switching the service world storage to Arch SHALL NOT change JoinWorld, observer, WorldSnapshot, WorldDelta, movement response, or failure response authority semantics.

#### Scenario: JoinWorld snapshot remains authoritative
- **WHEN** a client joins the world after the server is using Arch-backed storage
- **THEN** the JoinWorld snapshot is built from DG `GameWorld`
- **AND** the snapshot contains DG entity ids and final component values

#### Scenario: Failed move does not broadcast
- **WHEN** a movement action fails because of blocking, permission, policy, or conflict
- **THEN** the server returns failure to the requester according to existing response semantics
- **AND** it does not broadcast a changed WorldDelta solely because Arch storage evaluated the query

#### Scenario: Successful move broadcasts one authoritative delta
- **WHEN** an action commits a state change in Arch-backed storage
- **THEN** the sync system broadcasts a DG WorldDelta built from dirty state
- **AND** all observing clients receive the same authoritative result
