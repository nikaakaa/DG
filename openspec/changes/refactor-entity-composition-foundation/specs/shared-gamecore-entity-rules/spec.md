## ADDED Requirements
### Requirement: 实体组合构建边界
系统 SHALL 将实体组件组合规则放在 GameCore 的 archetype/build 边界中，而不是放在 `GameWorld` 容器内部。`GameWorld` MUST 只负责 entity 存储、component store、空间索引、dirty 和 snapshot/delta。

#### Scenario: GameWorld 不组装组件组合
- **WHEN** 通过配置创建 player、ball 或 blocker entity
- **THEN** 组件组合由 archetype/build 边界决定
- **AND** `GameWorld` 不通过 `HasPosition`、`HasCollider`、`HasAutoMove` 或类似布尔字段分支决定组件集合

#### Scenario: 新增组合不修改 GameWorld
- **WHEN** 后续新增一个由已有组件组成的实体 archetype
- **THEN** 系统通过新增或修改 archetype 配置表达组合
- **AND** 不需要修改 `GameWorld` 的实体创建代码

### Requirement: Archetype 与 Spawn Spec 分离
系统 SHALL 分离实体类型组合定义和实体实例生成参数。Archetype SHALL 描述 `ConfigId`、`ArchetypeId`、`EntityTarget`、组件 kind 集合和 tags；spawn/build spec SHALL 描述 `EntityId`、位置、方向、playerId、自动移动间隔等实例参数。

#### Scenario: 同一 archetype 创建多个实例
- **WHEN** 两个不同 player entity 使用同一个 player archetype 创建
- **THEN** 两个 entity 拥有相同组件组合
- **AND** 两个 entity 可以拥有不同 `EntityId`、坐标和 `PlayerControlComponent.PlayerId`

#### Scenario: 实例参数不污染 archetype
- **WHEN** 自动移动实体使用同一个 archetype 但以不同位置生成
- **THEN** 位置来自 spawn/build spec
- **AND** archetype 不为每个实例复制一份坐标配置

### Requirement: Luban 实体配置导出
系统 SHALL 直接通过 Luban 从 Excel 源表导出实体 archetype、world spawn 和 player spawn rule 配置，并通过生成的 provider/registry 作为正式实体配置来源。`GameWorld`、移动系统和空间系统 MUST NOT 直接依赖 Luban 生成类型。

#### Scenario: 导出基础实体配置
- **WHEN** 运行 Luban 导出命令
- **THEN** 生成结果包含 player、ball 和 blocker 的 archetype 数据
- **AND** 生成结果包含 demo world spawn 数据
- **AND** 生成结果包含 player spawn rule 数据

#### Scenario: 使用 Luban provider
- **WHEN** 服务端或客户端需要按 `ConfigId` 创建或应用实体
- **THEN** 运行时代码通过 Luban provider 查询 archetype
- **AND** `GameWorld` 不引用 Luban 生成命名空间或具体表类型

#### Scenario: 测试 fallback 不成为正式路径
- **WHEN** 测试环境需要不依赖 Luban 文件直接构造实体
- **THEN** 系统可以使用 fallback provider
- **AND** 正式服务端世界初始化不调用 `DefaultWorldConfig.CreatePlayer/CreateBall/CreateBlocker`

## MODIFIED Requirements
### Requirement: 组件驱动实体能力
系统 SHALL 用组件和 tag 表达实体能力，规则 MUST NOT 依赖 entity 名字或 demo 专用类型判断实体行为。实体能力组合 MUST 由 archetype/build 边界创建，并应用到 `GameWorld` 的 component store。

#### Scenario: 玩家实体能力
- **WHEN** 配置创建玩家 entity
- **THEN** 玩家 entity 拥有 `PositionComponent`
- **AND** 玩家 entity 拥有 `ColliderComponent`
- **AND** 玩家 entity 拥有 `BlockingComponent`
- **AND** 玩家 entity 拥有 `PlayerControlComponent`
- **AND** 这些组件由 entity builder 或等价组合边界应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 反弹球实体能力
- **WHEN** 配置创建自动反弹球 entity
- **THEN** 球 entity 拥有 `PositionComponent`
- **AND** 球 entity 拥有 `DirectionComponent`
- **AND** 球 entity 拥有 `ColliderComponent`
- **AND** 球 entity 拥有 `AutoMoveComponent`
- **AND** 球 entity 拥有 `BouncableComponent`
- **AND** 这些组件由 entity builder 或等价组合边界应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 阻挡体能力
- **WHEN** 配置创建墙或阻挡体 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `BlockingComponent`
- **AND** 这些组件由 entity builder 或等价组合边界应用，而不是由 `GameWorld` 内部 if 链组装
