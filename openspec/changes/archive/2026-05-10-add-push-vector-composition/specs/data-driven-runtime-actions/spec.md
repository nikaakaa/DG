## ADDED Requirements

### Requirement: 同 tick Push Vector Composition
系统 SHALL compose same-tick push contributions targeting the same resolved subject into a deterministic net push vector before those contributions become independent movement attempts. All push contributions SHALL enter the same default composition pool unless a future explicit composition-group policy is added. Composition MUST preserve contribution metadata and MUST NOT use repeated queued action count as the source of strength.

#### Scenario: 相反方向抵消
- **WHEN** the same resolved subject receives an `Up` push contribution and a `Down` push contribution for the same ready tick
- **THEN** the system computes a net vector whose vertical component is cancelled
- **AND** cancelled contributions do not both continue as independent push actions
- **AND** diagnostics preserve the contributing causality samples

#### Scenario: 同方向累加为贡献元信息
- **WHEN** the same resolved subject receives multiple same-direction push contributions for the same ready tick
- **THEN** the system keeps one composed push intent for that direction
- **AND** the composed intent records contribution count and per-direction contribution metadata
- **AND** the current behavior does not convert contribution count into extra movement distance

#### Scenario: 不同 push spec 默认合成
- **WHEN** different action specs produce push contributions for the same resolved subject and ready tick
- **THEN** those contributions enter the same default push composition pool
- **AND** the system does not require action spec grouping before composing ordinary push contributions

#### Scenario: 不同 subject 不合成
- **WHEN** two push contributions have the same ready tick and direction
- **AND** they target different resolved subjects
- **THEN** they remain separate composed intents
- **AND** one subject's contribution metadata does not affect the other subject

### Requirement: Push Energy Metadata Reservation
系统 SHALL reserve metadata fields for future push energy or strength policies without fully interpreting those fields in the first implementation. Same-direction contribution count SHALL be retained as future strength input, but it MUST NOT affect movement distance, priority, cost, or collision bypass until a future explicit strength policy is added.

#### Scenario: energy 不改变当前移动
- **WHEN** a composed push intent contains energy placeholder data
- **THEN** the push still resolves through the current one-step movement semantics
- **AND** same-direction contribution count and energy value do not produce multi-cell movement, higher priority, or collision bypass

#### Scenario: future policy has retained inputs
- **WHEN** future work introduces energy decay, strength, mass, or multi-step push
- **THEN** it can read total contribution, per-direction contribution, net vector, and causality samples from the composed push metadata
- **AND** it does not need to restore duplicate queued actions to recover contribution facts
