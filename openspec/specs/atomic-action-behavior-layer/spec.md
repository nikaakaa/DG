# atomic-action-behavior-layer Specification

## Purpose
TBD - created by archiving change refactor-atomic-action-behavior-layer. Update Purpose after archive.
## Requirements
### Requirement: Atomic Action Unit Lifecycle
系统 SHALL represent runtime behavior as action units with explicit lifecycle, cost tick, ready tick, arbitration, commit result, and retry state. A unit MUST NOT enter arbitration before its ready tick.

#### Scenario: Action waits for ready tick
- **WHEN** an action unit is created with `createdTick = 10` and `costTicks = 2`
- **THEN** its ready tick is 12
- **AND** the rule execution system does not arbitrate it before tick 12

#### Scenario: Ready action enters arbitration
- **WHEN** server tick reaches an action unit's ready tick
- **THEN** the action unit enters the shared arbitration flow
- **AND** arbitration reads current world state and final component results at that tick

#### Scenario: Unit identifiers are separated
- **WHEN** a player action derives a blocker action
- **THEN** the external request keeps its `OwnerActionId`
- **AND** each runtime behavior unit has a distinct `ActionUnitId`
- **AND** parent/derived relationships are represented without reusing one id for every unit

### Requirement: Unit-Internal Atomic Claims
系统 SHALL treat `ActionClaim` as the atomic boundary inside one action unit. A unit succeeds only when all required claims for that unit can be planned and committed; partial movement of the same unit MUST NOT be committed as success.

#### Scenario: Port-connected body moves atomically
- **WHEN** a port-connected body action unit is accepted
- **THEN** planner creates claims or move members for every body member
- **AND** commit succeeds only if every member move in that unit can be applied

#### Scenario: Unit claim failure rejects the unit
- **WHEN** any required claim in an action unit fails during planning or commit
- **THEN** the action unit result is failed, rejected, or pending according to its policy
- **AND** no partial success is reported for that unit

### Requirement: Derived Action Waiting And Retry
系统 SHALL model blocker-derived behavior as a relationship between action units. When a parent action unit is blocked by a pushable blocker, it MAY derive a blocker action unit and enter waiting state. The parent MUST retry after the derived unit succeeds instead of being replaced by the derived unit.

#### Scenario: Parent waits for derived blocker action
- **WHEN** a move action unit is blocked by a pushable entity
- **THEN** the arbiter creates a derived push action unit for the blocker
- **AND** the original move action unit enters waiting state
- **AND** the original action result is not marked successful yet

#### Scenario: Parent retries after derived success
- **WHEN** the derived blocker action unit succeeds
- **THEN** the parent action unit becomes ready to retry on a later ready tick
- **AND** the parent re-enters arbitration using current world state

#### Scenario: Parent fails after derived failure
- **WHEN** the derived blocker action unit fails
- **THEN** the waiting parent action unit fails or is rejected with a stable reason
- **AND** no parent movement is committed

### Requirement: Non-Transitive Push Blocking
系统 SHALL support bounded transitive pushable blocker resolution through action unit derivation and retry. When a derived push action is itself blocked by another pushable blocker, the derived action MAY create one child action unit and enter waiting state, as long as chain safety limits are respected. The system MUST NOT move any parent unit until its child succeeds and the parent retries on its own ready tick.

#### Scenario: Root waits for nested push chain
- **WHEN** a player move is blocked by pushable A
- **AND** A's derived push is blocked by pushable B
- **THEN** the player move waits for A's push action
- **AND** A's push action may wait for B's push action
- **AND** no parent movement is committed before its direct child succeeds and the parent retries

#### Scenario: Nested push moves in retry order
- **WHEN** B's derived action succeeds and frees A's target cell
- **THEN** A's action becomes ready to retry on a later ready tick
- **AND** A succeeds only if its retried claims are now valid
- **AND** the original player action succeeds only after it later retries and commits its own claims

#### Scenario: Nested push failure keeps parent unmoved
- **WHEN** any child action in a push chain fails
- **THEN** its direct parent action fails with a stable child failure reason
- **AND** upstream parent actions fail or remain pending according to pending state resolution
- **AND** no failed parent commits movement

#### Scenario: Chain safety stops unbounded propagation
- **WHEN** nested push derivation would exceed max depth, revisit the same subject, duplicate an active child, or exceed pending timeout
- **THEN** the current action unit fails with a stable safety reason
- **AND** no additional child action unit is created for that unsafe step

### Requirement: Port Push Uses Same Unit Mechanism
系统 SHALL handle port-connected body push through the same derived action waiting and retry mechanism used by ordinary pushable chains. Port-connected body push MUST NOT use a separate front-chain rule, a port-specific completion branch, or an execution-layer whole-chain solver.

#### Scenario: Linked member blocked by pushable
- **WHEN** a player is connected to a linked body member
- **AND** the linked member target cell contains a pushable blocker
- **THEN** the player body move action waits for a derived blocker push action
- **AND** the body move action retries after the blocker action succeeds

#### Scenario: Port body retries atomically
- **WHEN** the pushable blocker has moved away
- **THEN** the waiting port-connected body action retries
- **AND** all members of the body move atomically if the retried claims are valid

#### Scenario: Linked body pushes linked body
- **WHEN** connected body A-B moves right
- **AND** A-B is blocked by connected body C-D through a legal push entry on C-D
- **THEN** A-B waits for a derived C-D body action
- **AND** C-D moves as a connected body subject, not as ordinary single entity push members
- **AND** A-B retries only after C-D succeeds

### Requirement: Pending State Stores Action Units
系统 SHALL store pending action state in terms of parent action units, derived action units, retry state, body-aware chain metadata, and safety limits. Pending state MUST NOT use `CurrentFrontEntityId` as the primary behavior result model.

#### Scenario: Pending retains parent action
- **WHEN** an action unit enters waiting state
- **THEN** pending state retains enough data to retry the original action unit
- **AND** pending state records the derived action unit it waits for

#### Scenario: Pending completion waits for parent result
- **WHEN** a derived action unit succeeds
- **THEN** pending state does not mark the parent action complete
- **AND** the parent action remains pending until it retries and produces its own result

#### Scenario: One active child per unit
- **WHEN** a parent action unit waits for a derived action unit
- **THEN** the parent unit has at most one active derived child at that time
- **AND** additional blocked outcomes for that parent are evaluated only after retry

#### Scenario: Derived action may own a child
- **WHEN** a derived action unit is blocked by another legal pushable subject
- **THEN** the derived action unit may become a parent for one child action unit
- **AND** the chain records depth and visited subjects before creating the child

#### Scenario: Derived failure reports parent context
- **WHEN** a derived action unit fails
- **THEN** the parent action result records the blocker context
- **AND** the parent action result also preserves the child failure reason

### Requirement: Behavior Layer Reads Final Components
系统 SHALL make action unit arbitration read final `GameWorld` component/tag results only. The behavior layer MUST NOT query Ability, RuntimeEffect, or client-local state to decide server-authoritative behavior.

#### Scenario: Runtime effect influences behavior through final component
- **WHEN** a runtime effect changes final movement permission or port connector state
- **THEN** action arbitration observes the final component result
- **AND** arbitration does not query `RuntimeEffectStore` or effect kinds directly

#### Scenario: Client mirrors final server result
- **WHEN** server action units produce final world state and deltas
- **THEN** `ClientMapWorld` mirrors the server result
- **AND** client code does not locally arbitrate action unit success

### Requirement: Atomic Action Verification
系统 SHALL include automated validation for action unit lifecycle, split-tick derived behavior, port-connected body retry, bounded nested push propagation, and failure handling. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative verification, and Unity EditMode tests for atomic action units
- **AND** tests cover ordinary push retry, nested push retry, nested push failure, port-connected body retry, body pushes body, and chain safety guards

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** split-tick ordinary push, nested push, port-connected body push, and body-to-body push converge to the same server final state on both clients

