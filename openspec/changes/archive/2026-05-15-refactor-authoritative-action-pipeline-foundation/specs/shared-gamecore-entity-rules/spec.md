## ADDED Requirements

### Requirement: Authoritative Action Pipeline Foundation

Shared GameCore SHALL expose the authoritative action pipeline as explicit server-side data stages: `ActionContext -> TargetData -> ExecutionOutput -> Claim -> Arbitration -> Planning -> Commit`. Each stage MUST have a bounded responsibility, and server-authoritative behavior MUST NOT be decided by Unity client code, action-name string branches, entity-name branches, or hidden pending parent/child chains.

#### Scenario: Stage ownership is explicit
- **WHEN** a move-like action enters the shared rules pipeline
- **THEN** context construction owns source/target/cost/causality facts
- **AND** targeting owns read-only target data query
- **AND** execution owns structured output candidates
- **AND** claim arbitration owns conflict resolution
- **AND** planning and commit own final world mutation safety

#### Scenario: Client mirrors final result
- **WHEN** server action stages produce committed world changes
- **THEN** `ClientMapWorld` mirrors snapshot/delta results
- **AND** client code does not compute authoritative TargetData, ExecutionOutput, claim winners, blocked result, or final commit

#### Scenario: No hidden pending chain
- **WHEN** an action emits blocked, deferred, or multi-target output
- **THEN** continuation is represented through explicit action context and deferred output data
- **AND** the shared rules pipeline does not rely on removed `PendingRuleStates` as the ordinary push path

### Requirement: Front Target Movement Foundation

Shared GameCore SHALL support a minimal authoritative front-target movement foundation. A configured action MAY query a finite set of entities or occupied cells in front of its source, convert those target data items into execution output, generate one or more claims, arbitrate them, and commit only accepted plans.

#### Scenario: Move all front objects succeeds
- **WHEN** an action targets all movable objects in front of the source
- **AND** every required target movement claim can be accepted and committed
- **THEN** all required target movements are committed by the server
- **AND** observers receive final server delta rather than client-predicted positions

#### Scenario: Move all front objects blocked
- **WHEN** one required front target movement claim is blocked under default all-or-nothing policy
- **THEN** the action does not report overall success
- **AND** partial movement is not accepted unless explicit partial success policy is configured

#### Scenario: Target fanout stays deterministic
- **WHEN** multiple front targets are discovered
- **THEN** their target data and generated claims are ordered deterministically
- **AND** repeated runs with the same world state produce the same arbitration and commit result
