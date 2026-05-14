## ADDED Requirements

### Requirement: Targeting Precedes Execution And Claims
系统 SHALL run authoritative targeting before execution output and claim generation. Targeting MUST produce a read-only, deterministic `TargetData[]` candidate set from `ActionContext`, targeting selector, targeting policy, target filters, and `GameWorld`. Execution and claim generation MUST consume that candidate set rather than rediscovering target shape through action names or ad hoc spatial scans.

#### Scenario: move action consumes TargetData
- **WHEN** a ready move-compatible action enters arbitration
- **THEN** the targeting stage produces target data before move execution builds claims
- **AND** move execution consumes that target data to produce action claims
- **AND** planning and commit still validate accepted claims before writing world state

#### Scenario: Targeting 不裁决 blocked
- **WHEN** targeting returns a target cell or target entity that later proves occupied, blocked, reserved, or invalid for movement
- **THEN** blocked policy, arbitration, planning, or commit decides the result
- **AND** targeting does not derive push, bounce, reject, deferred output, or commit proposals

#### Scenario: selector 扩展不改仲裁职责
- **WHEN** a future `FrontLine`, `FrontEntities`, box, or circle selector class is added
- **THEN** it still returns candidate target data only
- **AND** arbitration, planning, and commit responsibilities remain unchanged

### Requirement: Deterministic Multi-Target Data
系统 SHALL make multi-target `TargetData[]` finite, bounded, and deterministically ordered. Multi-target output MUST include enough metadata for downstream fanout, diagnostics, and future effect binding without relying on dictionary order, Unity object order, client arrival order, or raw debug strings.

#### Scenario: selector stable ordering
- **WHEN** a selector returns several target entries for the same action
- **THEN** target data is ordered by explicit hit order and deterministic tie-breakers such as coordinate and entity id
- **AND** repeated runs over the same world state produce equivalent target data order

#### Scenario: multi-target fanout remains claim-driven
- **WHEN** a multi-target action produces several entity target data entries
- **THEN** execution may fan them out into multiple action claims
- **AND** each target claim still enters normal arbitration, planning, and commit
- **AND** target data alone does not move any entity

### Requirement: TargetData Metadata Boundary
`TargetData` SHALL describe candidate target facts such as target entity id, target coordinate, target body or subject key, hit cell, hit order, distance, direction, query id, and filter result. `TargetData` MUST NOT store pending lifecycle, action result ownership, commit state, or world mutation side effects.

#### Scenario: target body metadata supports later subject resolution
- **WHEN** targeting finds a target entity that belongs to a connected body
- **THEN** target data may include a body id or subject key for later layers
- **AND** subject resolution and connected body movement are still decided by subject policy, body capability resolution, claims, planning, and commit

#### Scenario: no world mutation
- **WHEN** targeting runs for any action
- **THEN** entity positions, components, tags, dirty state, runtime effects, and world delta buffers remain unchanged
- **AND** any world write must occur later through commit

### Requirement: Targeting Failure Is Structured
系统 SHALL return structured targeting failure when required target hints, direction, range, config, or filter inputs are invalid. Failure MUST carry stable error and reason data that action arbitration can map to action results without guessing from action names.

#### Scenario: missing direction
- **WHEN** an action requires direction from request but the context direction is `None`
- **THEN** targeting fails with stable invalid-direction data
- **AND** arbitration maps that failure to a rejected action result without executing claims

#### Scenario: target too far
- **WHEN** an action requires one-step target coordinate and the target is beyond one grid cell
- **THEN** targeting fails with stable too-far data
- **AND** no claim, plan, commit, or deferred output is created for that invalid target
