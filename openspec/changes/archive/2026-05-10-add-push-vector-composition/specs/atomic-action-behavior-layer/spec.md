## ADDED Requirements

### Requirement: Same-Tick Push Feedback Arbitration
系统 SHALL arbitrate same-tick push feedback before it can create persistent opposite-direction loops on the same subject. This arbitration MUST happen within the action behavior layer and MUST NOT be implemented as queue dedupe alone.

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
