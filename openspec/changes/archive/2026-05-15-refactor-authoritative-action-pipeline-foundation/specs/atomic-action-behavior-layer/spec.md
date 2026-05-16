## MODIFIED Requirements

### Requirement: Atomic Action Unit Lifecycle

系统 SHALL represent runtime behavior as action units with explicit context, lifecycle, cost tick, ready tick, targeting, execution output, arbitration, commit result, and finite deferred output state. A unit MUST NOT enter targeting or arbitration before its ready tick. Atomicity means one bounded action unit owns its result boundary; it does not mean the unit lacks source, target, or causality context.

#### Scenario: Action waits for ready tick
- **WHEN** an action unit is created with `createdTick = 10` and `costTicks = 2`
- **THEN** its ready tick is 12
- **AND** the rule execution system does not run targeting, execution output, or arbitration before tick 12

#### Scenario: Ready action enters targeting and arbitration
- **WHEN** server tick reaches an action unit's ready tick
- **THEN** the action unit builds TargetData from its ActionContext and current world state
- **AND** execution output and arbitration read final component results at that tick

#### Scenario: Unit identifiers are separated
- **WHEN** an external request creates a runtime action unit
- **THEN** the external request keeps its `OwnerActionId`
- **AND** each runtime behavior unit has a distinct action/unit id
- **AND** result ownership is represented through ActionContext rather than reusing one id for every derived or deferred unit

### Requirement: Unit-Internal Atomic Claims

系统 SHALL treat required claims generated from one ExecutionOutput as the atomic success boundary inside one action unit. A unit succeeds only when all required claims for that unit can be arbitrated, planned, and committed under its success policy. Partial movement of the same unit MUST NOT be committed as success unless explicit partial success policy is present.

#### Scenario: Port-connected body moves atomically
- **WHEN** a port-connected body action unit is accepted under all-or-nothing policy
- **THEN** planner creates claims or move members for every body member
- **AND** commit succeeds only if every required member move in that unit can be applied

#### Scenario: Multi target unit defaults to all-or-nothing
- **WHEN** one action context targets multiple front entities and emits required move claims
- **AND** any required claim is rejected or blocked
- **THEN** the action unit does not report overall success
- **AND** partial success is not inferred from the successful subset

#### Scenario: Unit claim failure rejects the unit
- **WHEN** any required claim in an action unit fails during arbitration, planning, or commit
- **THEN** the action unit result is failed, rejected, or deferred according to its policy
- **AND** no partial success is reported for that unit unless explicitly configured

## ADDED Requirements

### Requirement: Atomic Action Context Ownership

系统 SHALL keep source/result ownership inside ActionContext for atomic actions. Result attribution for success, block, bounce, noop, deferred output, direction change, and diagnostics MUST come from the same context and explicit policy data. The system MUST NOT use hidden parent/child waiting to rediscover which source should receive the result.

#### Scenario: Self push result owner is source
- **WHEN** AutoMove emits self push
- **THEN** the action context records the AutoMove entity as source and result owner
- **AND** blocked reversal or cadence commit targets that source according to configured commit policy

#### Scenario: Deferred output does not keep hidden parent
- **WHEN** an action emits structured deferred output for a future tick
- **THEN** the future output has its own action context
- **AND** it does not implicitly update the old source through hidden parent waiting

#### Scenario: Context does not replace policy
- **WHEN** context records source, target, direction, and owner
- **THEN** blocked, bounce, push, partial, and commit behavior still comes from `ActionSpec` or explicit policy data
- **AND** context fields are not interpreted as action-name-specific behavior
