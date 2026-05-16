## MODIFIED Requirements

### Requirement: Rule Pipeline File Boundaries
The rule pipeline SHALL keep action definitions, policy data models, request models, tag/component gates, subject selection, target selection, strategy registry, strategy modules, claim arbitration, planning, conflict resolution, deferred output enqueue, and commit in separate source file boundaries while preserving the same runtime behavior. No single central class SHALL own all ordinary behavior construction, blocked outcome execution, claim arbitration, and commit mutation.

#### Scenario: Action definitions are separate from execution
- **WHEN** a new `ActionSpec` default policy is reviewed
- **THEN** its source, required tags, blocked tags, cost, strategy, blocked result policy, merge policy, interrupt policy, plan rule, and commit rule are found in the action definition or policy boundary
- **AND** central arbitration execution is not the source of those policy values

#### Scenario: Strategy modules are separate from central arbitration
- **WHEN** a behavior needs a new reusable low-level capability
- **THEN** the code adds or updates a strategy module and registry entry
- **AND** the registry entry is produced from strategy metadata into explicit generated C# registration code
- **AND** central arbitration continues to compare produced action units and claims

#### Scenario: Generated registration is outside rule decisions
- **WHEN** generated strategy registration code is reviewed
- **THEN** it only maps typed strategy keys to strategy module construction or registration
- **AND** it does not contain ordinary action id branches, world-state decisions, tag-condition evaluation, planning, or commit logic
- **AND** rule systems consume the completed registry through dependency injection

#### Scenario: Planning and commit remain distinct
- **WHEN** accepted action claims are available
- **THEN** planning turns them into move/spawn/remove/component/effect proposals
- **AND** commit applies proposals atomically
- **AND** conflict resolution remains responsible for same-tick atomic commit decisions

#### Scenario: Deferred output is separate from commit
- **WHEN** an action emits deferred output
- **THEN** deferred output enqueue is represented as structured future action input
- **AND** commit does not own push propagation lifecycle
- **AND** removed pending retry code is not used as a fallback

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement and behavior rule code around system-layer responsibilities: action intake, action unit lifecycle, tag/component gate, subject selection, target selection, strategy execution, claim arbitration, planning, deferred output, conflict/commit, and result application. Component and tag reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, ordinary action ids, gameplay-specific names, Unity runtime objects, Fantasy runtime objects, protocol generated types, Ability state, RuntimeEffect state, editor scanning APIs, or runtime reflection to choose ordinary behavior strategy.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, mechanism push, configured wind push, or future ordinary behavior is evaluated
- **THEN** the rule system may read final components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names, demo-only entity type names, or ordinary action id strings to decide behavior strategy
- **AND** the rule system does not query Ability or RuntimeEffect state

#### Scenario: tag 只作为事实输入
- **WHEN** rules evaluate tags such as source, ability, state, immunity, or blocker tags
- **THEN** those tags are used only as facts for explicit policy conditions or filters
- **AND** tag combinations do not replace `ActionSpec`, strategy key, or reusable policy fields

#### Scenario: 规则职责拆分后行为保持一致
- **WHEN** the state-driven rule system processes the same world state and queued actions as before a pipeline refactor
- **THEN** it produces equivalent accepted action units, rejected reasons, deferred outputs, move plans, commit results, dirty changes, and owner action results for existing covered scenarios

### Requirement: Push Pending Boundary
Shared GameCore SHALL remove push propagation from parent-child pending handoff semantics. Push continuation MUST be represented as finite action unit output and deferred output that does not make the source action wait for downstream result success. The action policy pipeline MUST NOT recreate `PendingRuleStates`, `PendingActionState`, or `PendingActionUnit` as the default mechanism for push composition.

#### Scenario: Push does not create pending child handoff
- **WHEN** a source action is blocked and emits push output
- **THEN** the output is a structured deferred action or equivalent future ready action input
- **AND** the source action does not wait for the downstream action result through `PendingRuleStates`

#### Scenario: 组合不能通过旧 pending chain 完成
- **WHEN** same-tick composition, multi-contact fanout, closed-loop feedback, or long push propagation is evaluated
- **THEN** it is represented through finite current-tick claims and bounded deferred output
- **AND** it does not use parent retry or child completion from the removed pending chain

#### Scenario: Future waiting action requires a separate proposal
- **WHEN** a future feature truly needs a waiting action lifecycle
- **THEN** it requires a separate OpenSpec change with explicit lifecycle, tests, and manual verification
- **AND** it MUST NOT reuse the removed push pending chain as an implicit fallback
