# multiplayer-entity-management Specification

## Purpose
TBD - created by archiving change add-multiplayer-entity-management. Update Purpose after archive.
## Requirements
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

### Requirement: Session 与 Player Entity 绑定
系统 SHALL 维护 session 与 player entity 的双向绑定，并以该绑定作为移动权限和在线玩家枚举的依据。

#### Scenario: 查询已绑定 entity
- **WHEN** 移动 Handler 收到已 Join session 的移动请求
- **THEN** 多人管理器能返回该 session 绑定的 player entity id

#### Scenario: 清理不可用 session
- **WHEN** 在线枚举遇到已经不可用或 disposed 的 session
- **THEN** 系统从在线绑定和 observer 枚举中移除该 session
- **AND** 后续广播不再向该 session 发送消息

### Requirement: JoinWorld 协议主路径
系统 SHALL 通过 Outer 协议源和导出工具生成 JoinWorld 请求与响应，用于客户端获取自己的服务端分配 player entity。

#### Scenario: 生成服务端 JoinWorld 协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端生成目录包含 `C2G_JoinWorldRequest` 和 `G2C_JoinWorldResponse`
- **AND** 生成的 request 实现 `IRequest`
- **AND** 生成的 response 实现 `IResponse`

#### Scenario: 生成客户端 JoinWorld helper
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 客户端生成目录包含 `NetworkProtocolHelper.C2G_JoinWorldRequest(...)`
- **AND** 该 helper 通过 `Session.Call` 发送 RPC 请求

### Requirement: 移动请求权限校验
系统 SHALL 只允许 session 移动自己已绑定的 player entity，拒绝未 Join、未知 entity 和越权 entity 移动。

#### Scenario: 已绑定玩家移动自己的 entity
- **WHEN** 已 Join session 发送 `C2G_MoveRequest`
- **AND** request 中的 `EntityId` 等于该 session 绑定的 player entity id
- **THEN** 移动 Handler 调用权威移动世界裁决移动

#### Scenario: 未 Join session 发送移动
- **WHEN** 未 Join session 发送 `C2G_MoveRequest`
- **THEN** 移动 Handler 返回 `Success=false`
- **AND** 响应 reason 表示 session 尚未进入世界
- **AND** 服务端不广播 `G2C_EntityMovedNotify`

#### Scenario: 客户端试图移动其他玩家
- **WHEN** session A 发送 `C2G_MoveRequest`
- **AND** request 中的 `EntityId` 属于 session B
- **THEN** 移动 Handler 返回 `Success=false`
- **AND** 权威移动世界中 session B 的 player entity 坐标不变
- **AND** 服务端不广播 `G2C_EntityMovedNotify`

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

### Requirement: Unity 客户端本地玩家绑定
Unity 客户端 SHALL 使用服务端 JoinWorld 响应创建和绑定本地 player entity，而不是默认把多个客户端都绑定到 entity 1。

#### Scenario: Join 成功创建本地玩家
- **WHEN** Unity 客户端收到成功的 JoinWorld 响应
- **THEN** 客户端使用响应中的 `EntityId` 创建或绑定本地 player entity
- **AND** 后续移动输入使用该 entity id 提交

#### Scenario: Join 失败不允许移动
- **WHEN** Unity 客户端 JoinWorld 失败
- **THEN** 客户端不提交移动请求
- **AND** 客户端输出可验证失败日志

### Requirement: Unity 客户端显示多个玩家
Unity 客户端 SHALL 能在同一个客户端 world 中同时存在并显示本地 player entity 和远端 player entity。

#### Scenario: 收到远端玩家 snapshot
- **WHEN** 客户端收到一个不属于本地玩家的 player entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该远端 player entity

#### Scenario: 收到远端玩家移动通知
- **WHEN** 客户端收到远端 player entity 的 `G2C_EntityMovedNotify`
- **THEN** 客户端把该远端 player entity 更新到服务端最终坐标
- **AND** 客户端不执行本地移动规则来推导远端坐标

### Requirement: 三客户端端到端验收
系统 SHALL 提供手动端到端验证路径，证明至少三个客户端能代表三个不同 player entity 并互相看到移动结果。

#### Scenario: 三客户端分配不同 entity
- **WHEN** 服务端运行且客户端 A、B、C 依次 Join
- **THEN** 服务端日志显示 A、B、C 绑定到三个不同 player entity id
- **AND** 三个客户端都能看到三个 player entity

#### Scenario: 任一客户端移动同步到其他客户端
- **WHEN** 客户端 A 移动自己的 player entity
- **THEN** 客户端 B 和 C 收到该 entity 的服务端最终坐标
- **AND** B 和 C 显示的 A 坐标与服务端日志一致

#### Scenario: 占位冲突不污染其他客户端
- **WHEN** 客户端 A 尝试移动到客户端 B 的当前坐标
- **THEN** 服务端拒绝该移动
- **AND** 客户端 B 和 C 不应用 A 的错误目标坐标

