## ADDED Requirements
### Requirement: Body Push Chain Capability Boundary
系统 SHALL evaluate connected body push propagation through final components, connected body view, and body capability resolution. A connected body MAY be pushed by another action unit only through a legal touched member push entry, and the body MUST move as one action subject.

#### Scenario: body push entry is touched member
- **WHEN** connected body A-B has `PushableComponent` only on A
- **AND** another body pushes A
- **THEN** A is accepted as the push entry for A-B
- **AND** A-B may be moved as one connected body subject

#### Scenario: non-pushable member rejects body push
- **WHEN** connected body A-B has `PushableComponent` only on A
- **AND** another body pushes B
- **THEN** B is not accepted as the push entry for A-B
- **AND** A-B does not move because of A's component

#### Scenario: component does not propagate through chain
- **WHEN** connected body A-B pushes connected body C-D
- **AND** only C has `PushableComponent`
- **THEN** C-D may be pushed only when the touched blocker member is C
- **AND** D does not gain push entry behavior from C

### Requirement: Body Push Chain Verification Boundary
系统 SHALL verify body push chain behavior with Unity TestFramework EditMode tests and server authoritative verification. Unity Player build MUST NOT be required for automated validation, and end-to-end runtime sync MUST be left to manual Play Mode verification.

#### Scenario: automated body chain validation
- **WHEN** automated tests run
- **THEN** they cover connected body pushing connected body, connected body chain retry, non-pushable entry rejection, movement permission failure, cycle guard, max depth guard, and no partial body commit

#### Scenario: manual body chain validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** a horizontal row of pushable connected bodies can be pushed from the left
- **AND** the rightmost connected body remains a body subject rather than splitting into ordinary single entity push
- **AND** both clients observe the same final server WorldDelta
