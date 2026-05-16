## ADDED Requirements
### Requirement: Rotate Pivot Component Boundary
Shared GameCore SHALL model rotate-pivot behavior through an explicit final component on a connected body member. A connected body MAY enter rotate response only when its connected body view contains exactly one final rotate pivot component. The rotate pivot component SHALL mark a member of the same connected body and MUST NOT define an external anchor, player input action, persistent momentum state, or independent skill system. The final rotate pivot result MAY come from static entity configuration or from resolved runtime effect / buff component sources.

#### Scenario: exactly one pivot enables rotate response
- **WHEN** a connected body has exactly one member with final rotate pivot component
- **AND** the body receives a legal push entry
- **THEN** the rule layer may evaluate rotate response around that pivot member

#### Scenario: zero pivots does not enable rotate response
- **WHEN** a connected body has no final rotate pivot component
- **AND** the body receives a legal push entry
- **THEN** the body remains eligible for ordinary connected-body push behavior
- **AND** the rule layer does not invent a pivot from entity id, body root, action name, or tag combination

#### Scenario: multiple pivots invalidates rotate response
- **WHEN** a connected body contains more than one final rotate pivot component
- **AND** the body receives push
- **THEN** rotate response is invalid for that push
- **AND** the body does not move because of rotate response
- **AND** rotate response does not emit downstream deferred push output

#### Scenario: runtime pivot source enables rotate response
- **WHEN** a runtime effect contributes final rotate pivot component to one member of a connected body
- **AND** no other member has final rotate pivot component
- **AND** the body receives a legal push entry
- **THEN** the rule layer evaluates rotate response around the runtime-provided pivot member

### Requirement: Connected Body Torque Response
Shared GameCore SHALL allow a push that targets a connected body with exactly one rotate pivot to be consumed as a torque response. Torque response SHALL evaluate each push contribution against the touched member position relative to the pivot and the contribution push direction. Clockwise and counter-clockwise contributions SHALL be counted and offset by contribution count. A non-zero remaining side SHALL produce at most one 90 degree rotation in the current authoritative tick. A rotate-pivot body that consumes push in a tick MUST resolve to exactly one final behavior result for that resolved subject: rotate movement, deferred push output, ordinary translation when rotate response is not enabled, or invalid/no movement. It MUST NOT both rotate and translate in the same tick.

#### Scenario: single contribution produces clockwise rotation
- **WHEN** a push contribution touches a valid member of a rotate-pivot connected body
- **AND** the cross product of pivot-to-contact radius and push direction is clockwise
- **THEN** the body attempts one clockwise 90 degree rotation
- **AND** the original push is consumed by rotate response rather than ordinary translation

#### Scenario: same direction torque does not multiply rotation angle
- **WHEN** multiple push contributions produce the same remaining torque direction for one rotate-pivot connected body in the same ready tick
- **THEN** the body attempts one 90 degree rotation in that direction
- **AND** it does not rotate 180 degrees or farther because of contribution count

#### Scenario: opposite torque contributions offset
- **WHEN** one rotate-pivot connected body receives clockwise and counter-clockwise torque contributions in the same ready tick
- **THEN** the rule layer offsets them by contribution count
- **AND** if the remaining count is zero, the body does not rotate
- **AND** if one side remains, the body attempts one 90 degree rotation toward that side

#### Scenario: zero torque contribution invalidates rotate-pivot push
- **WHEN** a push contribution points through the pivot or otherwise produces zero torque
- **THEN** that contribution does not produce rotation
- **AND** it does not fall back to ordinary push translation for that rotate-pivot body
- **AND** it does not emit downstream deferred push output
- **AND** diagnostics may preserve its causality and contribution metadata

#### Scenario: offset torque invalidates rotate-pivot push
- **WHEN** clockwise and counter-clockwise torque contributions for a rotate-pivot connected body offset to zero in the same ready tick
- **THEN** the body does not rotate
- **AND** the consumed push does not translate the body
- **AND** the consumed push does not emit downstream deferred push output

### Requirement: Rotate Target Planning
Shared GameCore SHALL plan rotate-pivot connected body movement by rotating every body member around the pivot by exactly 90 degrees on the integer grid. The pivot member SHALL remain at its coordinate. Current cells occupied by the same rotating body SHALL be treated as releasable internal cells during target validation. Target validation MUST include both final target cells and the integer-grid swept cells between each member's current cell and rotated target cell. A rotate plan MUST fail before commit when two members resolve to the same target cell or when any target or swept cell is invalid. A successful rotate plan MUST rotate each member's orientation by the same 90 degree direction as its position.

#### Scenario: internal occupied cells are releasable
- **WHEN** member A's rotated target cell is currently occupied by member B of the same rotating body
- **AND** member B also has a valid rotated target cell
- **THEN** member A's target is not blocked solely by member B's current occupancy

#### Scenario: duplicate rotated targets cancel rotation
- **WHEN** two members of the same rotating body resolve to the same target coordinate after 90 degree rotation
- **THEN** the rotate plan fails
- **AND** no member position is committed

#### Scenario: pivot remains fixed
- **WHEN** a rotate-pivot connected body successfully rotates
- **THEN** the pivot member keeps its original coordinate
- **AND** every other moved member commits its rotated target coordinate in the same authoritative result
- **AND** every member with a `DirectionComponent` commits the corresponding 90 degree rotated direction

#### Scenario: swept blocker cancels rotation
- **WHEN** a rotate-pivot connected body member would sweep through an external blocker cell before reaching a free final target cell
- **THEN** the blocker is treated as a rotate impact contact
- **AND** the rotating body does not commit movement or direction changes
- **AND** downstream push output uses the swept impact cell as impact to-coordinate

### Requirement: Rotate Response Deferred Push Output
Shared GameCore SHALL treat external blockers hit by a rotate-pivot target plan as downstream push contacts. When all external blockers are pushable through existing body capability rules, rotate response SHALL emit deferred push output for every distinct downstream handoff subject and cancel the current rotate movement. Each deferred push direction MUST be derived from the exact impact member's rotational tangential displacement from its current cell to its rotated target cell. The deferred push MUST preserve push origin context equivalent to ordinary push context, including impact member, impact from/to, blocker, pivot, rotate direction, source action, and causality samples. When any external blocker is not pushable, rotate response SHALL cancel without moving itself and without emitting partial deferred output.

#### Scenario: blocked rotation emits all downstream pushes
- **WHEN** a rotate-pivot connected body attempts rotation
- **AND** multiple rotated target cells are occupied by distinct external pushable subjects
- **THEN** rotate response emits deferred push output for every distinct downstream subject
- **AND** the rotating body does not commit movement in the current tick
- **AND** each deferred push uses the impact member's own from-to tangent direction

#### Scenario: external connected body is pushed as a body
- **WHEN** a rotated target cell is occupied by a member of another connected body
- **AND** the touched member is a legal push entry for that external body
- **THEN** the deferred push output targets the external connected body subject
- **AND** it does not split that body into a single-member push
- **AND** the handoff preserves the impact member context that caused the connected body push

#### Scenario: non-pushable blocker cancels without partial output
- **WHEN** any external blocker hit by rotate response is not pushable under final component and body capability rules
- **THEN** the rotate response fails or noops without committing rotation
- **AND** it does not emit deferred push output for only the pushable subset

#### Scenario: same subject impact contexts survive merge
- **WHEN** two rotating members impact blockers that resolve to the same downstream subject and same push direction
- **THEN** the deferred push may merge into one downstream push request
- **AND** the merged request keeps both impact contribution contexts for later arbitration, diagnostics, and presentation metadata

### Requirement: Rotate Response Verification Boundary
Shared GameCore SHALL verify rotate-pivot connected body response with Unity TestFramework EditMode tests and server authoritative verification. Unity Player build MUST NOT be required for automated validation, and end-to-end runtime sync MUST be left to manual Play Mode verification.

#### Scenario: automated rotate response validation
- **WHEN** automated tests run
- **THEN** they cover unique pivot rotation, runtime effect pivot activation/removal, multiple pivot invalidation, torque accumulation, torque cancellation, internal cell release, duplicate target cancellation, impact-member tangent push direction, multi-blocker deferred push output, external connected body handoff, merged impact context preservation, and unchanged ordinary push vector behavior

#### Scenario: manual rotate response validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** a push-driven rotate-pivot connected body rotates or emits deferred push according to server results
- **AND** both clients observe the same final WorldDelta sequence

### Requirement: Rotate Pivot Presentation Metadata
Shared GameCore SHALL emit authoritative WorldDelta animation metadata for rotate-pivot presentation. Successful rotate-pivot movement SHALL emit one RotatePivot metadata entry per connected body member, including the pivot member, with pivot entity/coordinate, member from/to coordinate, rotate direction, and stable style key. Blocked rotate-pivot response SHALL emit one RotatePivotBounce metadata entry per connected body member with pivot coordinate, original coordinate, impact coordinate when available, rotate direction, and bounce flag; it SHALL also emit same-tick impact feedback metadata for the external blocker subject using the rotational tangent direction. Presentation metadata MUST NOT change the authoritative final snapshot and MUST remain deterministic across server and clients.

#### Scenario: successful rotate emits member metadata
- **WHEN** a rotate-pivot connected body successfully rotates
- **THEN** WorldDelta animation metadata contains one RotatePivot entry for each body member
- **AND** every entry includes the same pivot coordinate and rotate direction
- **AND** the pivot member entry keeps matching from/to coordinate while still carrying rotate direction for local orientation presentation

#### Scenario: blocked rotate emits bounce and impact feedback
- **WHEN** a rotate-pivot connected body hits an external pushable blocker and cancels its own movement
- **THEN** WorldDelta animation metadata contains RotatePivotBounce entries for rotating body members
- **AND** those entries keep final authoritative coordinates unchanged while exposing impact coordinates for visual bounce
- **AND** the impacted blocker subject receives same-tick impact feedback metadata in the rotational tangent direction

#### Scenario: clients play rigid rotate presentation
- **WHEN** Unity clients receive RotatePivot or RotatePivotBounce metadata
- **THEN** the animation layer can reconstruct a pivot-relative arc animation without inferring hidden rule state from snapshots alone
- **AND** connected body members preserve their relative shape visually during the rotate or bounce presentation
