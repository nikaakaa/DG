## MODIFIED Requirements
### Requirement: 服务端玩家实体分配
系统 SHALL 在客户端进入共享世界时由服务端分配唯一 player entity，并把该 entity 注册进服务端权威 GameWorld。该 player entity SHALL 使用共享 GameCore 组件表达玩家能力，而不是只注册为专用玩家坐标记录。玩家实体组件组合 SHALL 来自共享 GameCore 的 archetype/build 边界，而不是由服务端 Handler 或 `GameWorld` 内部硬编码组件 if 链决定。

#### Scenario: 第一个客户端进入世界
- **WHEN** 一个已连接 session 发送 JoinWorld 请求
- **THEN** 服务端分配一个唯一 player entity id
- **AND** 服务端通过共享 archetype/build 边界创建 player entity
- **AND** 服务端把该 entity 注册到权威 GameWorld
- **AND** 该 entity 拥有 `PositionComponent`、`ColliderComponent`、`BlockingComponent` 和 `PlayerControlComponent`
- **AND** 响应包含该 entity id 和初始坐标

#### Scenario: 三个客户端进入世界
- **WHEN** 三个不同 session 依次发送 JoinWorld 请求
- **THEN** 服务端为三个 session 分配三个不同 player entity id
- **AND** 三个 player entity 的初始坐标互不重叠
- **AND** 三个 player entity 都位于同一个权威 GameWorld
- **AND** 三个 player entity 使用同一个 player archetype 组合定义

#### Scenario: 同一 session 重复进入世界
- **WHEN** 已经 Join 成功的 session 再次发送 JoinWorld 请求
- **THEN** 服务端返回该 session 已绑定的 player entity id
- **AND** 服务端不额外创建新的 player entity
