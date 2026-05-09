## MODIFIED Requirements
### Requirement: State-driven 统一规则入口
系统 SHALL 将玩家输入、自动 tick、机关触发、运行时组件结果、调试命令和 push handoff 统一表示为服务端 tick 中的 behavior action unit，并通过统一的 state-driven rule system 修改 world 状态。旧 `MoveCommand` / `MovementResolveSystem` 路径 MAY remain only as a temporary compatibility or test helper during migration and MUST NOT be the long-term server-authoritative rule truth.

#### Scenario: 玩家输入生成 behavior action unit
- **WHEN** 已 Join 玩家请求向某方向移动
- **THEN** 服务端网络入口提交一个来源为玩家输入的 behavior action unit
- **AND** Handler 不直接修改 entity 坐标

#### Scenario: 自动移动生成 behavior action unit
- **WHEN** 服务端 tick 到达拥有 `AutoMoveComponent` 的 entity 的执行间隔
- **THEN** server-authoritative tick creates a behavior action unit from that entity's direction state
- **AND** auto movement does not directly bypass arbitration or commit

#### Scenario: 机关触发生成 behavior action unit
- **WHEN** 一个 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **THEN** server-authoritative tick creates a mechanism-source behavior action unit
- **AND** the mechanism effect does not directly modify the pushed entity coordinate

#### Scenario: behavior action unit 统一裁决
- **WHEN** the state-driven rule system processes ready behavior action units
- **THEN** it routes each unit through action intake, arbitration, result branch handling, planning when needed, and commit
- **AND** state changes record dirty data for snapshot/delta

#### Scenario: handoff action 结束源 unit
- **WHEN** a source behavior action unit hits a pushable target and the configured policy creates handoff
- **THEN** the source unit records a handoff result
- **AND** the source unit does not wait for the target unit to complete before writing its own result

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 behavior action unit，并交给统一的 action unit intake / arbitration / result / commit 管线裁决。

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **THEN** server-authoritative tick creates a mechanism-source behavior action unit for that entity
- **AND** the state-driven rule pipeline decides whether the entity may enter the right-side coordinate

#### Scenario: 阻挡目标格拒绝推动
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** the state-driven rule pipeline rejects or handoffs according to the configured policy
- **AND** 被推动 entity 保持原坐标 unless its own accepted action unit commits movement
- **AND** 推动系统不直接绕过阻挡规则修改坐标

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经有 active action unit 或 handoff chain guard
- **THEN** 该 tick 内其他进入推动地格不会为同一 entity 反复生成等价 action unit
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize rule code around system-layer responsibilities: behavior action unit intake, arbitration, result branch handling, planning, conflict/commit, and pending/handoff state. Component reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, demo-specific entity types, Unity runtime objects, Fantasy runtime objects, or protocol generated types.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, mechanism push, runtime result, or debug action is evaluated
- **THEN** the rule system may read components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names or demo-only entity type names to decide movement behavior

#### Scenario: 规则职责拆分后行为入口一致
- **WHEN** the state-driven rule system processes player input, auto tick, mechanism trigger, runtime result, debug command, or push handoff
- **THEN** every source enters through the same behavior action unit intake boundary
- **AND** source-specific differences are represented by source context, spec data, priority, tags, and result branch policy

#### Scenario: port-connected body 不定义普通 action 原子性
- **WHEN** a port-connected body is still supported during this change
- **THEN** it MAY remain a compatibility entity abstraction for covered port scenarios
- **AND** ordinary behavior action unit atomicity is not defined by multi-member body commit semantics

### Requirement: Rule Pipeline File Boundaries
The rule pipeline SHALL keep action unit intake, action spec data, arbitration, result branch handling, planning, conflict resolution, pending/handoff state, and rule execution in separate source file boundaries while preserving covered runtime behavior.

#### Scenario: Action unit intake is separate from arbitration execution
- **WHEN** a new action source such as runtime result, debug command, or handoff is reviewed
- **THEN** its mapping into behavior action unit input is found in the intake or adapter boundary
- **AND** the arbiter does not infer source defaults through scattered behavior-name branches

#### Scenario: Planning and commit remain distinct
- **WHEN** a behavior action unit is accepted for movement
- **THEN** planning produces a plan from accepted unit output before commit
- **AND** conflict resolution remains responsible for same-tick commit decisions

#### Scenario: Result branches are explicit
- **WHEN** a behavior action unit succeeds, fails, handoffs, or noops
- **THEN** the result branch is represented explicitly before dirty/delta or action result application
- **AND** source action success is not inferred from a later child retry completing
