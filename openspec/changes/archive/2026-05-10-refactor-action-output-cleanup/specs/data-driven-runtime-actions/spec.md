## ADDED Requirements

### Requirement: 进入推动输出配置化
系统 SHALL express `PushOnEnterComponent` output behavior through Luban Excel configuration data. The component application layer MUST NOT hard-code ordinary action spec ids such as `"mechanism_push"` to decide what behavior a PushOnEnter entity emits, and tests for configured behavior MUST use Luban-generated data rather than fallback defaults.

#### Scenario: PushOnEnter 从配置创建输出
- **WHEN** Luban Excel data declares an entity archetype with `PushOnEnter`
- **AND** it declares output action spec id and output cost ticks
- **THEN** entity construction creates `PushOnEnterComponent` from those configured values
- **AND** `ComponentApplicationRegistry` does not choose the output spec id by hard-coded string

#### Scenario: 不同行为复用 PushOnEnter 组件
- **WHEN** conveyor, wind field, or another tile-like entity uses `PushOnEnterComponent`
- **AND** each config row declares a different output action spec or cost
- **THEN** each entity emits the configured output action
- **AND** core rules do not add entity-name or action-name branches to distinguish them

#### Scenario: fallback 不作为统一数据源
- **WHEN** tests or runtime need PushOnEnter output data
- **THEN** they load Luban-generated config data
- **AND** they do not rely on fallback provider defaults to choose output spec or cost

### Requirement: 普通 action pipeline 不依赖 Pending
系统 SHALL allow ordinary ready actions and structured deferred outputs to execute through the action pipeline without a `PendingRuleStateStore`. Pending state MUST NOT be required for push continuation, PushOnEnter output, or deferred output re-entry.

#### Scenario: Server runner executes without pending push chain
- **WHEN** a server tick drains ready player, mechanism, auto, debug, or deferred actions
- **THEN** the main rule execution path processes them without creating pending child units for push continuation
- **AND** downstream push continuation is represented by deferred output
- **AND** source action results do not wait for downstream deferred result success

#### Scenario: Future waiting action is explicit
- **WHEN** a future behavior needs parent action result to wait for child action results
- **THEN** that behavior requires an explicit waiting-action proposal and policy
- **AND** it MUST NOT reuse legacy push pending chain as an implicit default path
