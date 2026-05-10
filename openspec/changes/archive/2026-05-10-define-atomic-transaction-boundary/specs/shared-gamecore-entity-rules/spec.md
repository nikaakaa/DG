## ADDED Requirements

### Requirement: Tick-Based Emergent Device Boundary
Shared GameCore SHALL model closed-loop and continuous devices as tick-based emergent behavior over finite atomic transactions. A device that continues to output push MUST do so by creating structured deferred outputs that later become action inputs, not by creating pending child push units or requiring one action to solve an unbounded chain.

#### Scenario: Closed loop output cadence
- **WHEN** a closed-loop connected body receives an input push with cost `a`
- **AND** its topology continues to produce an output after the first finite transaction
- **THEN** the next output is eligible no earlier than `currentTick + a`
- **AND** repeated output cadence is produced by repeated tick processing

#### Scenario: Device body may remain stationary
- **WHEN** a closed-loop connected body routes push back into itself
- **AND** there is no valid external movement commit for the body in the current transaction
- **THEN** the body is not required to move
- **AND** the device may still emit future push output according to explicit output policy
- **AND** the current transaction does not fail solely because the loop body did not translate

#### Scenario: Output target distinguishes no-output from deferred-output
- **WHEN** a closed-loop connected body routes feedback into itself
- **AND** no valid external output target exists
- **THEN** the transaction may complete with `bounded/no-output`
- **WHEN** a valid external output target exists
- **THEN** the transaction MUST record deferred output or an equivalent bounded deferred-output diagnosis
- **AND** it MUST NOT report `bounded/no-output`

#### Scenario: Surging device is not implicit strength amplifier
- **WHEN** a closed-loop connected body repeatedly emits output every `a` ticks
- **AND** no explicit strength policy is present
- **THEN** output strength does not grow simply because topology contains feedback
- **AND** future strength amplification requires explicit strength policy data

### Requirement: Shared Rule Truth For Atomic Emergence
Shared GameCore SHALL keep atomic transaction and emergent motion rules in the server-authoritative rule pipeline. Unity client mirror code MUST NOT locally decide loop continuation, same-tick push merging, or closed-loop output.

#### Scenario: Server owns loop continuation
- **WHEN** a loop device produces a future output action
- **THEN** the server-authoritative Shared GameCore rule pipeline decides that output
- **AND** Unity client state changes only through authoritative snapshot or delta results

#### Scenario: Manual sync validation
- **WHEN** two clients observe a loop device in Play Mode
- **THEN** both clients receive the same server-produced WorldDelta sequence
- **AND** neither client locally simulates extra loop output

### Requirement: Push Pending Boundary
Shared GameCore SHALL remove push propagation from parent-child pending handoff semantics. Push continuation MUST be represented as deferred output that does not make the source action wait for downstream result success.

#### Scenario: Push does not create pending child handoff
- **WHEN** a push action is blocked by a pushable downstream subject
- **THEN** the source action resolves its own finite transaction
- **AND** any downstream continuation is recorded as deferred output
- **AND** the source action does not wait for the downstream action result through `PendingRuleStates`

#### Scenario: Future waiting action requires a separate proposal
- **WHEN** a future action requires a parent action to wait for child action results
- **THEN** it MUST be specified separately from push propagation
- **AND** it MUST define finite child count, termination, cancellation, cycle, convergence, unsafe overlap, and owner result aggregation rules
- **AND** it MUST NOT reuse the removed push pending chain as an implicit fallback
