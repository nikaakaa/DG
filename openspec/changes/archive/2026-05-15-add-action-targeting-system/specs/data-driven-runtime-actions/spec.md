## ADDED Requirements

### Requirement: Targeting Policy Data Boundary
系统 SHALL express ordinary action target selection through explicit targeting policy data. `ActionSpec` MUST reference a resolved targeting policy or compatible migration rule, and runtime execution MUST NOT add ordinary behavior-name branches to choose target shape, direction source, filter, ordering, selector class, or empty-target handling.

#### Scenario: action 引用 targeting policy
- **WHEN** player move, auto move, mechanism push, debug move, or configured front multi-target behavior enters the rules layer
- **THEN** its target selection is resolved from `ActionSpec` targeting policy data
- **AND** the rule layer does not compare raw action names to choose target shape

#### Scenario: legacy target rule 只作迁移入口
- **WHEN** an existing action still uses `ActionTargetRule`
- **THEN** the registry or adapter maps it into equivalent targeting policy semantics
- **AND** new ordinary behavior does not require adding a new central `ActionTargetRule` branch
- **AND** new target algorithms are introduced as selector classes or extensions and then mapped from configuration

#### Scenario: selector class comes from config
- **WHEN** an action policy references a targeting selector id
- **THEN** the runtime resolves that id to a registered target selector class or extension
- **AND** changing the action alias does not change selector behavior

### Requirement: Targeting Config Import Resolution
系统 SHALL resolve targeting selector, targeting spec, and target filter authoring names into runtime identifiers or enums before authoritative rules consume them. Runtime action arbitration, execution, planning, and commit MUST NOT compare raw targeting names, selector names, filter names, tag strings, or action aliases to decide target behavior.

#### Scenario: 配置名在 provider 边界解析
- **WHEN** Luban data declares `targeting_selector_id`, `targeting_id`, `filter_id`, target tags, or readable target policy names
- **THEN** the provider resolves them into runtime identifiers, enums, or typed tag values
- **AND** `Shared.DG.GameCore` rule modules consume only the resolved values

#### Scenario: 未知 targeting 引用显式失败
- **WHEN** an action policy references an unknown targeting, selector, or filter id
- **THEN** registry construction fails with a clear error
- **AND** the action is not silently treated as player move, direction cell, self, or front entities

### Requirement: Target Filter Reads Final Facts
Target filter policy SHALL evaluate final `GameWorld` component, tag, and spatial facts only. It MUST NOT read RuntimeEffect source data, Unity presentation state, Fantasy session state, entity names, action names, or debug aliases to decide whether a target is included.

#### Scenario: final component 过滤
- **WHEN** a targeting policy filters for pushable positioned entities
- **THEN** the filter reads final `PositionComponent` and `PushableComponent` facts
- **AND** it does not inspect which RuntimeEffect or authoring source produced those final facts

#### Scenario: tag 过滤不替代策略字段
- **WHEN** a target has source, ability, state, immunity, or blocker tags
- **THEN** target filtering may include or exclude the target using resolved tag facts
- **AND** those tags do not choose target shape, subject policy, blocked policy, handoff policy, or commit behavior

### Requirement: Targeting Verification
系统 SHALL verify targeting policy data through OpenSpec validation, Shared build, server verification, and Unity TestFramework EditMode tests. End-to-end target hit behavior SHALL remain a manual Play Mode / two-client verification step.

#### Scenario: automated targeting validation
- **WHEN** automated validation runs
- **THEN** it includes `openspec validate`, Shared GameCore build, server authoritative verification, and Unity EditMode tests for targeting data import, selector registry mapping, self target, direction cell, target coord, filter evaluation, deterministic ordering, and unknown-id failure

#### Scenario: manual targeting validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** target selection results are observed only through server-produced snapshot/delta
- **AND** both clients converge to the same final server state for player move, auto move, and mechanism push
