## MODIFIED Requirements
### Requirement: Claim-Driven Action Arbitration
系统 SHALL use `BehaviorActionUnit + ActionSpec` to produce claim-driven arbitration results before planning or commit. `ActionClaim` MUST describe the current action unit's own claim over its source entity or current entity abstraction; ordinary action units MUST NOT claim a chain of unrelated body members as one default atomic commit.

#### Scenario: Move unit becomes claims
- **WHEN** a move behavior action unit enters arbitration
- **THEN** the arbiter resolves its `ActionSpec`, source context, target, direction, current unit entity abstraction, and final component/tag inputs
- **AND** the arbiter produces claims for the current action unit before planner creates a plan

#### Scenario: Execution does not decide move policies
- **WHEN** `StateDrivenRules` receives behavior action units for a tick
- **THEN** it delegates target resolution, blocker checks, merge, interrupt, blocked policy, and handoff decisions to the action arbiter or configured policy layer
- **AND** it only orchestrates intake, arbitration, result branch handling, planning, commit, pending/handoff state update, and result application

#### Scenario: Ordinary unit does not claim pushed target movement
- **WHEN** a source unit hits a pushable target
- **THEN** the source unit records a handoff result instead of claiming the target entity's movement in the same unit
- **AND** the target entity's movement requires its own derived behavior action unit

### Requirement: Port-Connected Body Claim Projection
系统 SHALL keep port-connected body projection isolated as compatibility semantics or a distinct entity abstraction. The arbiter MAY evaluate every member target cell for explicitly marked port-connected scenarios, but ordinary push and movement action unit atomicity MUST NOT depend on port-connected multi-member body projection.

#### Scenario: Linked port member hits pushable
- **WHEN** a player is port-connected to another blocking member
- **AND** the change explicitly treats that connected body as a compatibility entity abstraction
- **THEN** the arbiter may treat the connected body as the action unit's current abstraction
- **AND** this does not define the ordinary push handoff model for non-port actions

#### Scenario: Same body internal cells are not external blockers
- **WHEN** a connected body moves into cells previously occupied by members of the same body under compatibility coverage
- **THEN** those cells do not create external blocker claims
- **AND** the accepted action can remain a port-specific abstraction until a follow-up change defines the final port model

#### Scenario: Port model is not completed by this change
- **WHEN** this change is reviewed or validated
- **THEN** tests and docs must state that final port-connected body modeling is out of scope
- **AND** port compatibility must not be used to justify ordinary multi-entity action unit commits

### Requirement: Data-Driven Blocked Policy Resolution
系统 SHALL resolve blocked policies in the arbiter using `ActionSpec`, final component state, final tags, and claims. Execution and planner MUST NOT hardcode whether a blocked move becomes push, bounce, reject, or handoff.

#### Scenario: Start push if pushable creates handoff
- **WHEN** an action with `StartPushIfPushable` hits an external blocker with final pushable component state
- **THEN** the arbiter emits a handoff result for the source unit and creates or queues a target behavior action unit
- **AND** the source unit result reports handoff rather than waiting/pending success

#### Scenario: Bounce if bouncable
- **WHEN** an action with `BounceIfBouncable` hits an external blocker
- **AND** the moving entity has final bouncable component state
- **THEN** the arbiter emits the configured direction-change commit proposal for the current unit
- **AND** the execution layer does not contain bounce-specific business logic

#### Scenario: Non-pushable blocker fails current unit
- **WHEN** a source unit with push-on-block policy hits a blocker that is not pushable under final component state
- **THEN** the source unit fails with a stable blocked reason
- **AND** no parent retry is scheduled for that source unit

### Requirement: Accepted Actions Feed Planning
系统 SHALL pass only accepted behavior action units or accepted claims to planning. Planning MUST create plans from accepted arbitration output and MUST NOT rediscover pushable, bounce, blocked, or handoff policy outcomes.

#### Scenario: Planner consumes accepted unit claims
- **WHEN** the arbiter accepts a movement action unit
- **THEN** planner builds a plan for the current unit's source entity or current entity abstraction
- **AND** planner does not repeat blocked policy decisions already made by the arbiter

#### Scenario: Rejected or handoff action does not plan source movement
- **WHEN** the arbiter rejects a unit or resolves it as handoff
- **THEN** planner receives no move plan for that source unit's movement
- **AND** action result and pending/handoff state reflect the arbiter output

#### Scenario: Derived target unit plans independently
- **WHEN** a handoff creates a target behavior action unit
- **THEN** the target unit enters intake and arbitration as an independent unit
- **AND** its accepted plan or failure is not retroactively merged into the source unit's commit

### Requirement: Pending Push Uses Unified Action Flow
系统 SHALL express push continuation as behavior action unit inputs to the same claim-driven arbiter. Ordinary push MUST use handoff source results and target action units; pending state MAY remain only for scheduling, cycle protection, duplicate active action protection, timeout, and compatibility tracking.

#### Scenario: Handoff target arbitrates through claims
- **WHEN** a source unit handoffs to a pushable target
- **THEN** the target unit enters the same claim-driven arbitration flow as ordinary actions
- **AND** the source unit is already complete with handoff result

#### Scenario: Pending state keeps chain safety
- **WHEN** a handoff chain would loop, duplicate an active push, exceed retry/timeout protection, or hit a non-pushable external blocker
- **THEN** the arbiter or pending/handoff policy rejects or fails the relevant current unit with stable reason
- **AND** no source unit waits for a child unit to complete and retry source movement

#### Scenario: Nested push does not move earlier source units
- **WHEN** A handoffs to B and B handoffs to C
- **THEN** A and B both end as handoff results
- **AND** only the currently accepted target unit may commit its own movement

### Requirement: Claim Arbitration Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for action-unit arbitration. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative move verification, and Unity EditMode tests for action unit intake, claim generation, handoff creation, no parent retry, single-unit planning, conflict, interrupt, and compatibility guards

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** player move, auto move, mechanism push, push handoff, port compatibility cases, and WorldDelta observer sync remain consistent with server final state
- **AND** the manual report distinguishes OpenSpec validation, automated tests, server verification, and actual two-client runtime observation
