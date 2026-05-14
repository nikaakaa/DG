## MODIFIED Requirements

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

### Requirement: Unified Action Unit Lifecycle State Machine
系统 SHALL represent action unit lifecycle transitions with a unified finite state machine or equivalent centralized lifecycle module. The lifecycle SHALL support queued, ready, candidate-built, accepted, rejected, interrupted, planned, committed, deferred-output-emitted, completed, and failed outcomes as explicit states or transition results. Strategy modules and policy modules MUST NOT create independent hidden state machines for waiting, retry, or completion.

#### Scenario: 状态机推进有限阶段
- **WHEN** a queued action unit becomes ready
- **THEN** the unified lifecycle advances it through candidate build, arbitration, planning, commit, and completion stages as explicit transitions
- **AND** each transition has one bounded result

#### Scenario: 策略只能请求迁移
- **WHEN** a registered strategy produces claims, rejection, or deferred output
- **THEN** it returns a structured transition request or outcome
- **AND** the unified lifecycle records the resulting state
- **AND** the strategy does not store its own hidden waiting state

#### Scenario: 等待是显式扩展
- **WHEN** a future feature introduces waiting, retry, cancellation, or child-result dependency
- **THEN** the state machine adds explicit states and transition tests through a separate OpenSpec change
- **AND** the feature does not restore `PendingRuleStates`, `PendingActionState`, or `PendingActionUnit` as push default behavior
