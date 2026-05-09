## ADDED Requirements
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
系统 SHALL support one-level pushable blocker resolution through action unit derivation and retry. When a derived push action is itself blocked by another pushable blocker, the derived action MUST fail and the original owner action MUST complete as failed without moving any entity in that nested chain.

#### Scenario: Root waits for one derived push
- **WHEN** a player move is blocked by pushable A and A is blocked by pushable B
- **THEN** the player move may wait for A's push action
- **AND** A's push action MUST NOT wait for B's push action
- **AND** the owner action fails with a stable blocked result
- **AND** player, A, and B remain at their original coordinates

#### Scenario: Nested push does not move intermediate unit
- **WHEN** a push chain contains multiple blockers
- **THEN** the first derived unit that finds another pushable blocker is rejected
- **AND** the system does not move that derived unit before the downstream blocker moves

#### Scenario: Parent retries after one derived blocker moves
- **WHEN** the one-level derived blocker action succeeds and frees the target cell
- **THEN** the waiting parent action retries on a later ready tick based on its retry cost
- **AND** the parent succeeds only if its current claims are now valid

### Requirement: Port Push Uses Same Unit Mechanism
系统 SHALL handle port-connected body push through the same derived action waiting and retry mechanism used by ordinary pushable chains. Port-connected body push MUST NOT use a separate front-chain rule or a port-specific completion branch.

#### Scenario: Linked member blocked by pushable
- **WHEN** a player is connected to a linked body member
- **AND** the linked member target cell contains a pushable blocker
- **THEN** the player body move action waits for a derived blocker push action
- **AND** the body move action retries after the blocker action succeeds

#### Scenario: Port body retries atomically
- **WHEN** the pushable blocker has moved away
- **THEN** the waiting port-connected body action retries
- **AND** all members of the body move atomically if the retried claims are valid

### Requirement: Pending State Stores Action Units
系统 SHALL store pending action state in terms of parent action units, derived action units, retry state, and safety limits. Pending state MUST NOT use `CurrentFrontEntityId` as the primary behavior result model.

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
- **AND** the derived child fails rather than creating another push-derived child when it is blocked by another pushable entity

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
系统 SHALL include automated validation for action unit lifecycle, split-tick derived behavior, port-connected body retry, and failure handling. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative verification, and Unity EditMode tests for atomic action units
- **AND** tests cover ordinary one-level push retry, nested push failure, and port-connected body retry

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** split-tick ordinary push and port-connected body push converge to the same server final state on both clients
