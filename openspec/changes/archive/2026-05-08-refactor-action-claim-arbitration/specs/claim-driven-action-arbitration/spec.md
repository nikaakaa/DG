## ADDED Requirements
### Requirement: Claim-Driven Action Arbitration
系统 SHALL use `ActionRequest + ActionSpec` to produce claim-driven arbitration results before planning or commit. `ActionClaim` MUST be the primary runtime data for move conflict, body movement, target occupancy, same-claim merge, priority interrupt, and pending continuation decisions.

#### Scenario: Move request becomes claims
- **WHEN** a move `ActionRequest` enters arbitration
- **THEN** the arbiter resolves its `ActionSpec`, source context, target, direction, body, and body member movement
- **AND** the arbiter produces claims for the body members before planner creates a `MovePlan`

#### Scenario: Execution does not decide move policies
- **WHEN** `StateDrivenRules` receives actions for a tick
- **THEN** it delegates target resolution, blocker checks, merge, interrupt, and blocked policy decisions to the action arbiter
- **AND** it only orchestrates adapter, arbitration, planning, commit, pending state update, and result application

### Requirement: Port-Connected Body Claim Projection
系统 SHALL project port-connected bodies as a single claim group. The arbiter MUST evaluate every member target cell while ignoring old cells occupied by members of the same body.

#### Scenario: Linked port member hits pushable
- **WHEN** a player is port-connected to another blocking member
- **AND** the player's root move target is inside the connected body but the linked member target cell contains a pushable external blocker
- **THEN** the arbiter treats the blocker as the move blocker for the action
- **AND** the configured blocked policy may derive a push continuation without special execution-layer code

#### Scenario: Same body internal cells are not external blockers
- **WHEN** a connected body moves into cells previously occupied by members of the same body
- **THEN** those cells do not create external blocker claims
- **AND** the accepted action can plan an atomic body move

### Requirement: Data-Driven Blocked Policy Resolution
系统 SHALL resolve blocked policies in the arbiter using `ActionSpec`, final component state, final tags, and claims. Execution and planner MUST NOT hardcode whether a blocked move becomes push, bounce, or reject.

#### Scenario: Start push if pushable
- **WHEN** an action with `StartPushIfPushable` hits an external blocker with final pushable component state
- **THEN** the arbiter derives or updates pending push state
- **AND** the original action result reports push pending or equivalent configured result

#### Scenario: Bounce if bouncable
- **WHEN** an action with `BounceIfBouncable` hits an external blocker
- **AND** the moving entity has final bouncable component state
- **THEN** the arbiter emits the configured direction-change commit proposal
- **AND** the execution layer does not contain bounce-specific business logic

### Requirement: Data-Driven Priority And Behavior Policy
系统 SHALL treat action priority, source, required tags, blocked tags, blocked policy, conflict policy, merge policy, interrupt policy, plan rule, and commit rule as data supplied by `ActionSpec` or the action spec registry. Core execution, planning, and commit orchestration MUST NOT hardcode ordinary behavior priorities or ordinary behavior policies.

#### Scenario: Priority comes from action spec
- **WHEN** a player move, auto move, mechanism push, configured wind push, or debug action is created
- **THEN** the request priority is resolved from its `ActionSpec` or explicit runtime override allowed by that spec
- **AND** the arbiter uses that priority data for merge and interrupt decisions

#### Scenario: Behavior policy comes from action spec
- **WHEN** a behavior changes from reject-on-block to start-push-if-pushable or bounce-if-bouncable
- **THEN** the change is expressed by updating behavior config or registry data
- **AND** `StateDrivenRules`, planner, and commit orchestration do not add behavior-name branches

### Requirement: Accepted Actions Feed Planning
系统 SHALL pass only accepted actions or accepted claims to planning. Planning MUST create plans from accepted arbitration output and MUST NOT rediscover pushable, bounce, or blocked policy outcomes.

#### Scenario: Planner consumes accepted claims
- **WHEN** the arbiter accepts a body move action
- **THEN** planner builds `MovePlan` members from accepted body move claims
- **AND** planner does not repeat blocked policy decisions already made by the arbiter

#### Scenario: Rejected action does not plan
- **WHEN** the arbiter rejects or derives a pending continuation from an action
- **THEN** planner receives no move plan for that rejected source action
- **AND** action result and pending state reflect the arbiter output

### Requirement: Pending Push Uses Unified Action Flow
系统 SHALL express pending push continuation as `ActionRequest` inputs to the same claim-driven arbiter. Pending push MUST NOT use execution-layer special branches for port front, chain expansion, or final move acceptance when those can be represented through reusable arbitration policies.

#### Scenario: Pending push continuation arbitrates through claims
- **WHEN** a pending push reaches its next step tick
- **THEN** the pending state produces a push continuation request
- **AND** the request enters the same claim-driven arbitration flow as ordinary actions

#### Scenario: Pending push keeps chain safety
- **WHEN** a pending push continuation would loop, duplicate an active push, or hit a non-pushable external blocker
- **THEN** the arbiter or pending policy rejects or fails the continuation with stable reason
- **AND** existing covered push outcomes remain compatible

### Requirement: Ordinary Behavior Extension Does Not Modify Core Orchestration
系统 SHALL allow new ordinary move-like behavior to reuse existing primitives, claim kinds, conflict policies, merge policies, interrupt policies, blocked policies, and commit policies without editing `StateDrivenRules`, `IntentArbiter`, `RulePlanner`, or commit orchestration.

#### Scenario: Add configured wind push
- **WHEN** a configured `wind_push` behavior uses existing move primitive and claim policies
- **THEN** adding or changing the behavior requires only data/registry/test updates
- **AND** no new execution or arbitration orchestration branch is required

#### Scenario: New reusable policy is explicit core extension
- **WHEN** a behavior needs a policy not representable by existing policy types
- **THEN** adding that policy is treated as a core extension
- **AND** the extension includes focused tests proving existing policies remain unchanged

### Requirement: Claim Arbitration Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for claim-driven arbitration. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative move verification, and Unity EditMode tests for claim generation, body projection, push derivation, merge, conflict, interrupt, and accepted planning

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** player move, auto move, mechanism push, port-connected push, and WorldDelta observer sync remain consistent with server final state
