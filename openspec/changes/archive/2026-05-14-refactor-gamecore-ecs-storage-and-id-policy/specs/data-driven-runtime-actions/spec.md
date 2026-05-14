## ADDED Requirements

### Requirement: Action Runtime Uses Resolved IDs
The runtime action model SHALL use resolved action and policy identifiers after configuration import. `ActionRequest`, `WorldAction`, deferred output, pending state, arbitration, planning, and commit MUST NOT require raw action spec strings to choose ordinary behavior strategy.

#### Scenario: action 输入只携带解析后 ID
- **WHEN** a configured action is enqueued from player input, auto movement, push-on-enter output, debug tools, or deferred output
- **THEN** the action carries a resolved action spec identifier
- **AND** the registry resolves that identifier to policy data without comparing ordinary behavior names in rule code

#### Scenario: deferred output 等价键不依赖 debug 字符串
- **WHEN** equivalent deferred outputs are merged
- **THEN** equivalence uses resolved spec id, ready tick, direction, and resolved subject key
- **AND** parent causality id, debug string, raw contact count, or readable action alias do not decide equivalence

#### Scenario: 未解析 ID 显式失败
- **WHEN** runtime code attempts to enqueue or resolve an action using an unknown or unresolved action identifier
- **THEN** Shared GameCore fails with a clear error
- **AND** the action is not silently treated as player move, player push, mechanism push, or debug move

### Requirement: Ordinary Behavior String Branch Ban
Runtime action execution SHALL reject or flag new ordinary behavior strategy branches that compare raw action names, policy names, entity names, or string tag combinations. The ban applies to action intake after registry lookup, arbitration, planning, commit, pending, deferred output, and result application.

#### Scenario: 新普通行为只改数据和测试
- **WHEN** a new ordinary behavior uses existing primitive, source, target, blocked result, subject, handoff, conflict, merge, interrupt, plan, commit, and cost policies
- **THEN** it is added through configuration/provider data and tests
- **AND** no core runtime branch compares its readable behavior name

#### Scenario: 表现层字符串例外
- **WHEN** client animation, editor UI, sandbox authoring, logs, or diagnostics need readable names
- **THEN** they MAY use readable aliases or debug names
- **AND** those aliases do not affect server-authoritative arbitration, planning, commit, pending, or final coordinates
