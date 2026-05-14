## ADDED Requirements

### Requirement: Client Mirrors Arch-Backed Server Authority
Unity client SHALL remain a mirror and presentation layer when the Fantasy server uses Arch-backed authoritative storage. The client MUST consume DG WorldSnapshot, WorldDelta, and animation metadata; it MUST NOT run Arch storage as a second authoritative world.

#### Scenario: Snapshot applies DG ids
- **WHEN** the client receives a JoinWorld snapshot from an Arch-backed server world
- **THEN** the client applies DG entity ids and component values to `ClientMapWorld`
- **AND** it does not need Arch entity handles

#### Scenario: Delta applies final server result
- **WHEN** the client receives a WorldDelta from an Arch-backed server world
- **THEN** the client updates mirror state to the server final component values
- **AND** it does not re-run blocked, push, handoff, connected body, or runtime effect rules locally

#### Scenario: Client has no Arch authority path
- **WHEN** Unity presentation or debug UI needs to show movement, push, connected body, or runtime effect results
- **THEN** it reads DG mirror state and metadata
- **AND** it does not query an Arch authoritative world to decide final coordinates

### Requirement: Client Playback Speed Follows Server Delta Metadata
Client visual playback SHALL follow the server-authoritative tick, WorldDelta order, animation metadata, and presentation config. Client playback MAY interpolate or catch up visually, but MUST land on the authoritative final state from the latest server delta.

#### Scenario: Movement animation follows authoritative delta
- **WHEN** the client receives movement metadata for a changed entity
- **THEN** playback may animate from displayed position to authoritative final position
- **AND** the final displayed position equals the server delta result

#### Scenario: Continuous server ticks do not create local authority
- **WHEN** new deltas arrive before previous visual playback completes
- **THEN** the client may restart, blend, compress, or skip presentation according to presentation policy
- **AND** it must not invent an unconfirmed final coordinate

#### Scenario: Metadata-only push feedback stays non-authoritative
- **WHEN** the client receives push feedback metadata without a changed entity snapshot
- **THEN** it may play feedback animation
- **AND** it must not change mirror coordinates for that entity
