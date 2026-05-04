## ADDED Requirements
### Requirement: 进入推动能力组件
系统 SHALL 使用通用 `PushOnEnterComponent` 表达进入或停留在地格上会触发推动的能力，MUST NOT 使用 `ConveyorComponent` 这类物体种类组件作为核心规则判断。

#### Scenario: 传送带由通用组件组合
- **WHEN** 配置创建传送带 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `DirectionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `PushOnEnterComponent`
- **AND** 该 entity 不拥有 `BlockingComponent`
- **AND** 传送带种类通过配置标识或 tag 表达

#### Scenario: 推动能力不是业务种类
- **WHEN** 后续配置创建风场、水流或弹簧地格
- **THEN** 这些 entity 可以复用 `PushOnEnterComponent`
- **AND** 规则系统不依赖 entity 名字判断是否推动

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 `MoveCommand`，并交给 `MovementResolveSystem` 统一裁决。

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **THEN** `PushOnEnterSystem` 生成向右的 `MoveCommand`
- **AND** `MovementResolveSystem` 裁决该 entity 是否可以进入右侧坐标

#### Scenario: 阻挡目标格拒绝推动
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** `MovementResolveSystem` 拒绝该移动
- **AND** 被推动 entity 保持原坐标
- **AND** 推动系统不直接绕过阻挡规则修改坐标

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经被 `PushOnEnterSystem` 推动过
- **THEN** 该 tick 内其他进入推动地格不会再次推动该 entity
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动
