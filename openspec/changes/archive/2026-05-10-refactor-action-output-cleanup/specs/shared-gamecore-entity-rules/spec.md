## MODIFIED Requirements

### Requirement: 进入推动能力组件
系统 SHALL 使用通用 `PushOnEnterComponent` 表达进入或停留在地格上会触发推动的能力，MUST NOT 使用 `ConveyorComponent` 这类物体种类组件作为核心规则判断。`PushOnEnterComponent` 的输出 action spec 和输出 cost SHALL come from Luban Excel entity/component configuration rather than a hard-coded action id in component application code.

#### Scenario: 传送带由通用组件组合
- **WHEN** 配置创建传送带 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `DirectionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `PushOnEnterComponent`
- **AND** 该 entity 不拥有 `BlockingComponent`
- **AND** 传送带种类通过配置标识或 tag 表达
- **AND** 传送带输出 action spec 和 cost 来自 Luban 生成配置数据

#### Scenario: 推动能力不是业务种类
- **WHEN** 后续配置创建风场、水流或弹簧地格
- **THEN** 这些 entity 可以复用 `PushOnEnterComponent`
- **AND** 规则系统不依赖 entity 名字判断是否推动
- **AND** 组件应用层不通过写死 `"mechanism_push"` 或其他普通 action id 决定输出行为
- **AND** fallback provider 不作为正式 PushOnEnter 输出数据来源

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 state-driven mechanism source action input，并交给统一的 action unit / arbitration / plan / commit / deferred-output 管线裁决。PushOnEnter output MUST use configured output action data and MUST NOT bypass the action pipeline or create push pending child units.

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **AND** the `PushOnEnterComponent` declares a configured output action spec
- **THEN** server-authoritative tick creates a mechanism source move or push action input for that entity from the configured output
- **AND** the state-driven rule pipeline decides whether the entity may enter the right-side coordinate

#### Scenario: 阻挡目标格拒绝或输出 deferred
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** the state-driven rule pipeline rejects, emits deferred output, or fails the movement according to `ActionSpec` policy and final Component state
- **AND** 被推动 entity 不会被 execution 层直接绕过阻挡规则修改坐标
- **AND** push continuation does not create pending child units

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经 consumed a ready action subject or produced a deferred output
- **THEN** 该 tick 内其他进入推动地格不会再次直接推动该 entity
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动
