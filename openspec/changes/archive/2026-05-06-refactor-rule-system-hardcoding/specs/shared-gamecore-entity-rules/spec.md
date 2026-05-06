## ADDED Requirements
### Requirement: Intent 策略定义注册表
Shared GameCore SHALL use a single intent definition registry to define the default source tag, ability tag, required tags, blocked tags, and cancel policy for every supported `BehaviorIntentKind`. The system MUST NOT infer these defaults through multiple independent switch statements, and an unknown intent kind MUST fail explicitly instead of silently falling back to player semantics.

#### Scenario: 现有 intent kind 拥有显式定义
- **WHEN** Shared GameCore creates a `BehaviorIntent` for `Move`, `Push`, `AutoMove`, `MechanismPush`, or `DebugMove`
- **THEN** its default source tag, ability tag, blocked tags, required tags, and cancel policy come from the intent definition registry
- **AND** the resulting arbitration behavior matches the current player move, player push, auto move, mechanism push, and debug move behavior

#### Scenario: 未注册 intent kind 显式失败
- **WHEN** code attempts to create or resolve a `BehaviorIntent` whose kind has no registered definition
- **THEN** Shared GameCore fails with a clear error
- **AND** the intent is not treated as `SourcePlayer` by default

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement rule code around system-layer responsibilities: action intake, intent creation, arbitration, planning, conflict/commit, and pending state. Component reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, demo-specific entity types, Unity runtime objects, Fantasy runtime objects, or protocol generated types.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, or mechanism push is evaluated
- **THEN** the rule system may read components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names or demo-only entity type names to decide movement behavior

#### Scenario: 规则职责拆分后行为保持一致
- **WHEN** the state-driven rule system processes the same world state and queued actions as before the refactor
- **THEN** it produces the same accepted intents, rejected reasons, move plans, commit results, dirty changes, and move results for existing covered scenarios

### Requirement: Legacy movement resolver 迁移边界
Shared GameCore SHALL migrate production rule execution away from `Movement/Legacy/Systems.cs` before deleting or isolating that file. The server-authoritative path MUST use the state-driven action / intent / plan / commit pipeline as the rule truth, and legacy resolver APIs MUST NOT remain required by server-authoritative runtime construction after migration.

#### Scenario: Legacy 删除前完成依赖迁移
- **WHEN** `Movement/Legacy/Systems.cs` is removed or isolated
- **THEN** no production server-authoritative code depends on `MovementResolveSystem`, `AutoMoveSystem`, or `PushOnEnterSystem`
- **AND** equivalent state-driven tests cover the movement, auto movement, push-on-enter, port-connected body, and conflict cases previously validated through the legacy resolver

#### Scenario: 服务端权威路径不构造 legacy resolver
- **WHEN** the authoritative move world provider creates the server tick runner
- **THEN** the server-authoritative construction path does not require a `MovementResolveSystem`
- **AND** auto move and mechanism push are generated as state-driven actions or intents before arbitration and commit

## MODIFIED Requirements
### Requirement: State-driven 统一规则入口
系统 SHALL 将玩家输入、自动 tick 和后续机关移动统一表示为服务端 tick 中的 action 或 intent，并通过统一的 state-driven rule system 修改 world 状态。旧 `MoveCommand` / `MovementResolveSystem` 路径 MAY remain only as a temporary compatibility or test helper during migration and MUST NOT be the long-term server-authoritative rule truth.

#### Scenario: 玩家输入生成移动 action
- **WHEN** 已 Join 玩家请求向某方向移动
- **THEN** 服务端网络入口提交一个来源为玩家输入的 movement action
- **AND** Handler 不直接修改 entity 坐标

#### Scenario: 自动移动生成自动 action
- **WHEN** 服务端 tick 到达拥有 `AutoMoveComponent` 的 entity 的执行间隔
- **THEN** server-authoritative tick creates an auto movement action or intent from that entity's direction state
- **AND** auto movement does not directly bypass arbitration or commit

#### Scenario: 移动 action 统一裁决
- **WHEN** the state-driven rule system processes queued movement actions
- **THEN** it creates behavior intents, arbitrates by body/tag/priority/policy, plans accepted movement, and commits through the conflict resolver
- **AND** state changes record dirty data for snapshot/delta

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 state-driven mechanism push action 或 intent，并交给统一的 action / intent / plan / commit 管线裁决。

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **THEN** server-authoritative tick creates a mechanism push action or intent for that entity
- **AND** the state-driven rule pipeline decides whether the entity may enter the right-side coordinate

#### Scenario: 阻挡目标格拒绝推动
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** the state-driven rule pipeline rejects the movement
- **AND** 被推动 entity 保持原坐标
- **AND** 推动系统不直接绕过阻挡规则修改坐标

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经处于 pending push 或已被推动
- **THEN** 该 tick 内其他进入推动地格不会再次推动该 entity
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动
