## ADDED Requirements

### Requirement: Authoritative Action Context

系统 SHALL represent each server-authoritative action with an explicit `ActionContext` or equivalent runtime context. The context MUST distinguish instigator, source entity, optional causer, subject entry, target hint, direction, spec id, owner action id, causality, created tick, ready tick, and cost tick. The context MUST NOT decide behavior policy by itself and MUST NOT create implicit parent/child waiting.

#### Scenario: Player move context
- **WHEN** a player move input enters the authoritative action queue
- **THEN** the created context records the player as instigator, source entity, and subject entry
- **AND** it records the target hint or direction supplied by input
- **AND** it resolves behavior through `ActionSpec` rather than action-name branches

#### Scenario: AutoMove self push context
- **WHEN** an AutoMove entity reaches its cost tick
- **THEN** the created context records the AutoMove entity as instigator, source entity, and subject entry
- **AND** it records direction from final `DirectionComponent`
- **AND** it does not create pending parent/child state to remember the source

#### Scenario: Context is not world state
- **WHEN** arbitration needs current position, tags, components, blocking, or pushability
- **THEN** it reads those facts from final `GameWorld` state at the current tick
- **AND** it does not reuse stale world facts embedded inside `ActionContext`

### Requirement: TargetData Targeting Output

系统 SHALL express targeting/query results as `TargetData` or an equivalent finite target-data set before claim generation. TargetData MUST be a read-only candidate target description, not a commit, not a claim, and not a final result. The first supported target data policies MUST include Self, DirectionCell, and a front-cell/front-entity query sufficient for moving all objects in front of a source.

#### Scenario: Self targeting
- **WHEN** an action uses Self targeting
- **THEN** targeting emits one TargetData item for the action subject entry
- **AND** later execution may turn that TargetData into claims according to configured strategy

#### Scenario: DirectionCell targeting
- **WHEN** an action uses DirectionCell targeting with a valid direction
- **THEN** targeting emits one TargetData item for the adjacent cell in that direction
- **AND** the TargetData records the source query id and direction

#### Scenario: Front entities targeting
- **WHEN** an action uses the minimal front-entities targeting policy
- **THEN** targeting emits a finite ordered TargetData set for matching entities or occupied cells in front of the source
- **AND** ordering is deterministic and does not depend on dictionary enumeration order

### Requirement: ExecutionOutput Contract

系统 SHALL convert `ActionContext` plus TargetData into `ExecutionOutput` or an equivalent structured execution result before claim arbitration. ExecutionOutput MAY include claim candidates, blocked outcome candidates, deferred output candidates, commit candidates, result owner mapping, and success policy. ExecutionOutput MUST NOT directly mutate `GameWorld`.

#### Scenario: Move execution creates claim candidates
- **WHEN** a move-compatible execution consumes one or more TargetData items
- **THEN** it emits move claim candidates for the resolved subject or target subjects
- **AND** it does not directly change entity positions

#### Scenario: Blocked output stays structured
- **WHEN** execution detects or receives a blocked outcome candidate
- **THEN** it emits structured blocked/deferred/result data for the policy layer
- **AND** it does not hardcode push, bounce, or reject behavior by action id

#### Scenario: Commit still owns world mutation
- **WHEN** ExecutionOutput contains commit candidates or accepted claims
- **THEN** planning and commit validation still decide whether `GameWorld` changes
- **AND** failed validation prevents mutation even if execution produced candidates

### Requirement: Multi Target Success Policy Entry

系统 SHALL provide an explicit success policy entry for multi-target execution. The default policy MUST be all-or-nothing for required claims. Partial success MUST require explicit policy support and MUST NOT occur accidentally because some generated claims committed while others failed.

#### Scenario: Default all-or-nothing
- **WHEN** one action produces required claims for multiple TargetData items
- **AND** any required claim fails arbitration, planning, or commit
- **THEN** the action result is not reported as overall success
- **AND** no partial success is treated as the default behavior

#### Scenario: Partial success requires policy
- **WHEN** an action spec or execution policy does not explicitly allow partial success
- **AND** only some target claims can succeed
- **THEN** the system rejects or fails the unit according to all-or-nothing rules
- **AND** it does not silently report partial success

#### Scenario: Partial policy has data entry
- **WHEN** a future action needs partial success
- **THEN** the success behavior is represented through explicit success policy data and per-target result mapping
- **AND** adding that behavior does not require central action-name branching
