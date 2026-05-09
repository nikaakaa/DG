## MODIFIED Requirements
### Requirement: ActionSpec 策略定义注册表
Shared GameCore SHALL use `ActionSpec` as the behavior policy registry for authoritative movement actions. `ActionSpec` MUST define primitive, source, priority, tags, target rule, blocked policy, conflict policy, interrupt policy, merge policy, plan rule, commit rule, and subject policy for ordinary runtime behavior. Existing `ActionSubjectKind` values MAY remain as the implementation name during migration, but their required semantics are `HitEntity` for single entity subject and `ConnectedBodyIfAny` for connected body subject. `ActionSpecId` MUST be used only to look up policy data; rules MUST NOT infer subject behavior from action names.

#### Scenario: 现有行为拥有显式 ActionSpec
- **WHEN** player move, player push, auto move, mechanism push, debug move, configured wind push, or connected body move enters the rules layer
- **THEN** its default source tag, ability tag, blocked tags, required tags, target rule, blocked policy, conflict policy, interrupt policy, merge policy, plan rule, commit rule, and subject policy come from `ActionSpec`
- **AND** the rules layer does not require `BehaviorIntentKind` or an intent definition registry
- **AND** the rules layer does not branch on the `ActionSpecId` string to choose subject behavior

#### Scenario: 新行为不新增业务 kind
- **WHEN** a new ordinary move-like behavior is added
- **THEN** it is expressed through `ActionSpec`, source context, action unit lifecycle, and policy data
- **AND** it does not require a new `WorldActionKind` or `BehaviorIntentKind`
- **AND** it does not require a new `connected_body_xxx` action solely to opt into connected body subject selection

#### Scenario: 未注册 ActionSpec 显式失败
- **WHEN** runtime code attempts to resolve an unknown `ActionSpecId`
- **THEN** Shared GameCore fails with a clear error
- **AND** the action is not treated as player movement by default

#### Scenario: subject policy 可迁移到现有配置
- **WHEN** an existing move-like `ActionSpecId` is configured with `ConnectedBodyIfAny`
- **AND** the entry entity belongs to a port connected body
- **THEN** its resolved `ActionSpec` allows the rules layer to resolve the connected body view as the action subject
- **AND** no action-name-specific branch is required for that source

#### Scenario: WorldTag 不替代 subject policy
- **WHEN** an entity has source, ability, state, immunity, or blocker tags
- **THEN** those tags may be used as rule inputs or filters
- **AND** they do not cause connected body subject expansion unless the resolved `ActionSpec` subject policy allows it

#### Scenario: CompositeEntity policy deferred
- **WHEN** subject policy is extended in this change
- **THEN** `CompositeEntityIfAny` remains a future design boundary
- **AND** Shared GameCore does not create a persistent composite entity for ordinary port connections

## ADDED Requirements
### Requirement: Action Subject Resolver Boundary
Shared GameCore SHALL resolve the action subject from entry entity, resolved `ActionSpec` subject policy, final component/tag state, and port connected body view. Subject resolution MUST be read-only and MUST NOT perform planning, blocker resolution, handoff creation, commit, or world mutation.

#### Scenario: resolver produces single entity subject
- **WHEN** an action's resolved subject policy is `HitEntity`
- **THEN** subject resolution returns only the entry entity as the action subject
- **AND** port connectivity does not expand the subject for that action

#### Scenario: resolver produces connected body subject
- **WHEN** an action's resolved subject policy is `ConnectedBodyIfAny`
- **AND** the entry entity belongs to a port connected body with multiple members
- **THEN** subject resolution returns the connected body view as the action subject
- **AND** the connected body remains a runtime view, not a persistent world entity

#### Scenario: resolver ignores action name for subject choice
- **WHEN** two move-like actions have different `ActionSpecId` values but the same resolved subject policy
- **THEN** subject resolution uses the same subject selection behavior for both actions
- **AND** differences between those actions come from explicit `ActionSpec` fields, not hidden name branches

#### Scenario: resolver is read-only
- **WHEN** subject resolution runs
- **THEN** it does not modify `GameWorld`
- **AND** it does not create a `MovePlan`
- **AND** it does not inspect target blocker outcomes
- **AND** it does not create a handoff action
