## ADDED Requirements

### Requirement: 等价 Deferred Output 入队合并
系统 SHALL merge equivalent deferred outputs before they become queued runtime actions. Two deferred outputs are equivalent when they have the same ready tick, action spec id, direction, and resolved subject key. Equivalence MUST NOT depend on parent causality id, created tick, raw contact count, or the debug/logging dedupe string. Merged outputs MUST preserve an equivalent contribution count for diagnostics and future strength policy, but this change SHALL NOT interpret that count as force, strength, priority, or movement distance.

#### Scenario: 同 tick 同 subject 同方向合并
- **WHEN** multiple parent actions in the same tick produce deferred push outputs with the same ready tick
- **AND** those outputs have the same action spec id, direction, and resolved subject key
- **THEN** the runtime action queue contains at most one ready action for that equivalent deferred output
- **AND** different parent causality ids do not create duplicate queued actions
- **AND** the queued output records the number of equivalent contributions merged into it

#### Scenario: 不同方向不合并
- **WHEN** two deferred push outputs target the same resolved subject and ready tick
- **AND** one output direction is `Up` while the other output direction is `Down`
- **THEN** both deferred outputs remain observable as distinct queued actions
- **AND** no cancellation, interruption, or direction conflict decision is applied by deferred dedupe

#### Scenario: 不同 subject 不合并
- **WHEN** two deferred push outputs have the same ready tick, action spec id, and direction
- **AND** they resolve to different subject keys
- **THEN** both outputs remain queued independently
- **AND** multi-contact fanout to distinct downstream subjects is preserved

#### Scenario: 贡献数不改变当前强度
- **WHEN** equivalent deferred outputs are merged
- **THEN** their contribution count is retained for diagnostics and future policy
- **AND** the current runtime does not convert the contribution count into stronger push, longer movement, higher priority, or extra queued actions

### Requirement: Deferred Output 诊断摘要
系统 SHALL provide bounded diagnostics for deferred output enqueue. Diagnostics MUST show enough information to identify deferred growth without logging every repeated action in an explosive fanout scenario.

#### Scenario: 重复 deferred 输出摘要
- **WHEN** one tick produces multiple equivalent deferred outputs
- **THEN** diagnostics report raw deferred count, enqueued count, merged count, contribution count, and representative equivalent keys
- **AND** diagnostics avoid expanding every duplicate action and touched entity in the log

#### Scenario: 小规模 deferred 仍可追踪
- **WHEN** one tick produces a small number of deferred outputs
- **THEN** diagnostics still include spec id, entity id, subject key, direction, ready tick, cost, and causality sample
- **AND** the user can trace a single push chain across ticks
