# atomic-action-behavior-layer Specification

## Purpose
TBD - created by archiving change refactor-atomic-action-behavior-layer. Update Purpose after archive.
## Requirements
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
系统 SHALL include automated validation for action unit lifecycle, strategy registration, generated explicit strategy registration code, tag gate filtering, split-tick deferred behavior, port-connected body action units, bounded push propagation, same-tick composition, no old pending chain, and failure handling. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative verification, and Unity EditMode tests for atomic action units and the Action Policy Pipeline
- **AND** tests cover ordinary config-only behavior, attribute-declared strategy generation, generated registry injection, registered strategy behavior, multi-contact fanout, same-subject collapse, closed-loop feedback deferral, and chain safety guards
- **AND** tests prove the runtime tick path does not use reflection scanning to discover strategy classes

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** configured ordinary behavior, generated-registered strategy behavior, split-tick push, port-connected body push, and feedback composition converge to the same server final state on both clients
- **AND** the client does not locally decide strategy, claim arbitration, or final authoritative coordinates

### Requirement: Atomic Transaction Consumption Boundary
系统 SHALL treat each server tick's ready action processing as a finite atomic transaction boundary. Within one transaction, a resolved action subject MUST consume at most one merged push input, and any downstream push output produced by that consumption MUST NOT be consumed again by the same subject inside the same transaction. Atomic transaction composition MUST be represented by current-tick action units, claims, contribution composition, and deferred output; it MUST NOT be represented by an unbounded same-tick solver or by the removed parent-child pending chain.

#### Scenario: Same subject consumes once per tick
- **WHEN** two ready action units in the same server tick both resolve push input into the same subject
- **THEN** the subject consumes at most one merged input for that tick
- **AND** the system does not create two independent movement commits for the same subject
- **AND** no entity in that subject is committed by two action units in the same transaction

#### Scenario: Feedback waits for future tick
- **WHEN** a consumed push output would route back to a subject already consumed in the current transaction
- **THEN** the system does not consume that feedback again in the current transaction
- **AND** any continuing output is represented as a structured deferred output no earlier than `tick + cost`
- **AND** the current transaction remains finite

#### Scenario: 不恢复旧 pending chain
- **WHEN** atomic transaction composition needs to carry output into a later tick
- **THEN** it uses deferred output or equivalent future ready action input
- **AND** it does not create `PendingActionState`, `PendingActionUnit`, parent retry, or child completion records for push propagation

### Requirement: Emergent Motion Across Atomic Transactions
系统 SHALL model continuous or infinite-appearing motion as repeated finite transactions across ticks. A closed-loop device MUST NOT require a single action or pending state to recursively solve the complete loop before committing a result.

#### Scenario: Loop produces periodic output
- **WHEN** an external push activates a closed-loop device
- **AND** the loop still has a valid output path after one finite transaction
- **THEN** the current transaction commits only its finite result
- **AND** the next output is evaluated in a later tick according to the configured cost
- **AND** repeated outputs emerge from repeated tick processing rather than one unbounded action chain

#### Scenario: Loop without output does not hang
- **WHEN** a closed-loop device routes push back into itself without any external output
- **THEN** the transaction does not recursively expand forever
- **AND** the loop subject is not repeatedly consumed in the same transaction
- **AND** the transaction completes with a bounded no-output result instead of remaining active indefinitely

#### Scenario: Surging push device emits periodic output
- **WHEN** an external push activates a closed-loop device with a valid external output port
- **AND** each push has cost `a`
- **THEN** the first transaction consumes the loop subject once
- **AND** feedback to the loop subject is not consumed again in the same transaction
- **AND** the system records one deferred output with `ready tick = current tick + a`
- **AND** repeated output cadence is produced by later ready outputs, not by same-transaction recursion

#### Scenario: No first-phase strength amplification
- **WHEN** a closed-loop device feeds back into itself
- **AND** no explicit strength policy is configured
- **THEN** repeated outputs do not increase push strength across ticks
- **AND** the device is treated as a periodic output device rather than a push amplifier

### Requirement: Atomic Transaction Verification
系统 SHALL include automated validation for single-consumption subject behavior, feedback deferral, and closed-loop finite transaction boundaries. Unity Player build MUST NOT be required.

#### Scenario: Automated validation
- **WHEN** automated validation runs
- **THEN** it includes Unity TestFramework EditMode coverage for same-subject repeated push input, closed-loop feedback truncation, and no duplicate same-tick subject commits
- **AND** it includes Shared GameCore build and server authoritative verification

#### Scenario: Manual validation
- **WHEN** the user manually runs server-authoritative Play Mode with a closed-loop push device
- **THEN** the device does not freeze one action in an unbounded pending chain
- **AND** any continuing output is observed as later tick results
- **AND** two clients observe the same server WorldDelta sequence

### Requirement: Deferred Queue Growth Boundary
系统 SHALL keep cross-tick deferred output processing finite per tick by merging equivalent queued outputs. This boundary MUST prevent queue growth caused only by duplicate same-subject same-direction deferred outputs from multiple parent branches, while preserving periodic closed-loop output and distinct-subject fanout.

#### Scenario: 分支汇合同质输出不膨胀
- **WHEN** several propagation branches in one tick all produce a deferred push for the same resolved subject, same direction, same spec id, and same ready tick
- **THEN** only one equivalent queued output is consumed on that ready tick
- **AND** the number of queued actions does not grow solely because parent branch causality ids differ

#### Scenario: 周期结构仍跨 tick 运行
- **WHEN** a closed-loop device produces one deferred output per period
- **THEN** that output may still re-enter the queue on its future ready tick
- **AND** dedupe does not stop a periodic device merely because it has emitted in a previous tick

#### Scenario: 不替代 push 仲裁
- **WHEN** equivalent deferred dedupe runs
- **THEN** it does not decide interruption, cancellation, opposite-direction resolution, strength stacking, TTL expiry, or energy decay
- **AND** those policies require separate proposals if needed

### Requirement: Same-Tick Push Feedback Arbitration
系统 SHALL arbitrate same-tick push feedback before it can create persistent opposite-direction loops on the same subject. This arbitration MUST happen within the action behavior layer through contribution composition and claim arbitration. It MUST NOT be implemented as queue dedupe alone, as a central action-name branch, as an unbounded whole-chain solver, or as a revival of the old pending chain.

#### Scenario: 同 subject 反馈不形成双向 action 对
- **WHEN** a feedback structure produces same-tick opposite-direction push contributions for the same resolved subject
- **THEN** the behavior layer composes those contributions into one net vector result
- **AND** it does not enqueue both directions as independent ready push actions for that same subject

#### Scenario: 跨 tick 传播仍然保留
- **WHEN** a composed push intent produces a non-zero result and is blocked by a downstream pushable subject
- **THEN** the system may still emit deferred output for a future tick
- **AND** the future tick output is based on the composed result rather than every raw same-tick contribution

#### Scenario: composition 不是全链同 tick 求解
- **WHEN** a long push structure spans multiple bodies or cycles
- **THEN** same-tick composition only resolves contributions that are ready in the current tick
- **AND** it does not solve the entire future chain inside one tick
- **AND** it does not keep parent actions pending until every future child succeeds

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

