## MODIFIED Requirements
### Requirement: 服务端玩家实体分配
系统 SHALL 在客户端进入共享世界时由服务端分配唯一 player entity，并把该 entity 注册进服务端权威 GameWorld。该 player entity SHALL 使用共享 GameCore 组件表达玩家能力，而不是只注册为专用玩家坐标记录。

#### Scenario: 第一个客户端进入世界
- **WHEN** 一个已连接 session 发送 JoinWorld 请求
- **THEN** 服务端分配一个唯一 player entity id
- **AND** 服务端把该 entity 注册到权威 GameWorld
- **AND** 该 entity 拥有 `PositionComponent`、`ColliderComponent`、`BlockingComponent` 和 `PlayerControlComponent`
- **AND** 响应包含该 entity id 和初始坐标

#### Scenario: 三个客户端进入世界
- **WHEN** 三个不同 session 依次发送 JoinWorld 请求
- **THEN** 服务端为三个 session 分配三个不同 player entity id
- **AND** 三个 player entity 的初始坐标互不重叠
- **AND** 三个 player entity 都位于同一个权威 GameWorld

#### Scenario: 同一 session 重复进入世界
- **WHEN** 已经 Join 成功的 session 再次发送 JoinWorld 请求
- **THEN** 服务端返回该 session 已绑定的 player entity id
- **AND** 服务端不额外创建新的 player entity

### Requirement: 多玩家占位规则
系统 SHALL 通过服务端权威 GameWorld 的空间查询和 `BlockingComponent` 阻止不同 player entity 占用同一个格子。

#### Scenario: 目标格已有其他玩家
- **WHEN** player A 请求移动到 player B 当前坐标
- **THEN** 服务端通过 GameWorld 查询到目标格存在 player B
- **AND** 服务端因 player B 拥有 `BlockingComponent` 拒绝该移动
- **AND** player A 和 player B 的坐标都保持不变
- **AND** 响应包含明确的业务错误码和 reason

#### Scenario: 目标格为空
- **WHEN** player A 请求移动到相邻空格
- **THEN** 服务端允许移动
- **AND** player A 的坐标更新为目标格

### Requirement: Join 后自动观察共享世界
系统 SHALL 在 JoinWorld 成功后把 session 加入共享世界 observer 集合，并向该 session 同步当前权威 GameWorld 的统一 WorldSnapshot，使客户端无需手动声明观察某个 entity 才能接收多人和自动实体同步。

#### Scenario: 新客户端 Join 后接收已有玩家
- **WHEN** 客户端 B Join 成功
- **AND** 客户端 A 的 player entity 已存在
- **THEN** 客户端 B 能收到 A 的当前 player entity 坐标 WorldSnapshot

#### Scenario: 已有客户端收到新玩家
- **WHEN** 客户端 C Join 成功
- **AND** 客户端 A 和 B 已在线
- **THEN** 客户端 A 和 B 能收到 C 的 player entity 出现或坐标 WorldSnapshot

#### Scenario: 新客户端 Join 后接收自动实体
- **WHEN** 客户端 B Join 成功
- **AND** 权威 GameWorld 中已存在自动移动球或阻挡体
- **THEN** 客户端 B 能收到这些 entity 的 WorldSnapshot
- **AND** 自动实体 snapshot 与玩家 snapshot 使用同一套同步边界
