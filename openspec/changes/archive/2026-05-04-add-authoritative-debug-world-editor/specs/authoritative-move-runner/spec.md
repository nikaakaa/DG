## ADDED Requirements
### Requirement: WorldDelta 删除广播协议
系统 SHALL 通过 Outer 协议源和协议导出工具扩展 `G2C_WorldDeltaNotify`，使服务端能够在统一 delta 广播中表达被删除的 entity id。

#### Scenario: 生成服务端删除 delta 字段
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端生成的 `G2C_WorldDeltaNotify` 包含 `RemovedEntityIds`
- **AND** `RemovedEntityIds` 是可承载多个 entity id 的集合字段

#### Scenario: 生成客户端删除 delta 字段
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 客户端生成的 `G2C_WorldDeltaNotify` 包含 `RemovedEntityIds`
- **AND** 客户端 push handler 可以读取该集合

#### Scenario: 删除实体广播给 observer
- **WHEN** 服务端权威 GameWorld 删除一个 entity
- **AND** 在线 observer 集合非空
- **THEN** 服务端广播 `G2C_WorldDeltaNotify`
- **AND** notify 的 `RemovedEntityIds` 包含被删除的 entity id
- **AND** notify 的 `Entities` 不需要包含被删除 entity 的 snapshot

#### Scenario: 空删除不广播
- **WHEN** 服务端尝试删除不存在的 entity id
- **THEN** 权威 GameWorld 不产生 removed delta
- **AND** 服务端不因为该请求广播空的 `G2C_WorldDeltaNotify`

### Requirement: 调试编辑权限开关
服务端 SHALL 提供最小调试编辑权限开关。所有调试建造、调试拖拽和调试删除请求在修改权威 GameWorld 前 MUST 检查该开关。

#### Scenario: 调试开关关闭时拒绝建造
- **WHEN** `DebugWorldEditEnabled` 关闭
- **AND** 客户端发送调试建造请求
- **THEN** 服务端返回失败响应
- **AND** 响应 reason 表示调试编辑已关闭
- **AND** 权威 GameWorld 不新增 entity
- **AND** 服务端不广播 WorldDelta

#### Scenario: 调试开关关闭时拒绝拖拽
- **WHEN** `DebugWorldEditEnabled` 关闭
- **AND** 客户端发送调试拖拽请求
- **THEN** 服务端返回失败响应
- **AND** 权威 GameWorld 中目标 entity 坐标不变
- **AND** 服务端不广播 WorldDelta

#### Scenario: 调试开关关闭时拒绝删除
- **WHEN** `DebugWorldEditEnabled` 关闭
- **AND** 客户端发送调试删除请求
- **THEN** 服务端返回失败响应
- **AND** 权威 GameWorld 中目标 entity 仍然存在
- **AND** 服务端不广播 WorldDelta

### Requirement: 服务端权威调试编辑协议
系统 SHALL 通过独立调试 RPC 表达建造、拖拽和删除意图。调试 RPC MUST NOT 复用普通玩家移动协议。

#### Scenario: 生成调试建造协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端和客户端生成 `C2G_DebugSpawnEntityRequest` 与响应类型
- **AND** 客户端生成对应 helper
- **AND** 请求包含 config id、坐标、方向以及必要的调试实体参数

#### Scenario: 生成调试拖拽协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端和客户端生成 `C2G_DebugMoveEntityRequest` 与响应类型
- **AND** 请求包含 entity id 和目标坐标
- **AND** 该请求语义为调试传送而不是普通 MoveCommand

#### Scenario: 生成调试删除协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端和客户端生成 `C2G_DebugRemoveEntityRequest` 与响应类型
- **AND** 请求包含待删除 entity id

### Requirement: 服务端权威调试编辑执行
服务端 SHALL 在调试编辑权限允许时，把建造、拖拽和删除请求应用到权威 GameWorld，并通过统一 WorldDelta 广播给在线 observer。

#### Scenario: 调试建造产生权威实体
- **WHEN** 调试开关开启
- **AND** 客户端发送合法调试建造请求
- **THEN** 服务端通过 `EntitySpawnSpec` 在权威 GameWorld 创建 entity
- **AND** 服务端响应包含成功状态和最终 entity id
- **AND** 服务端广播包含该 entity snapshot 的 WorldDelta

#### Scenario: 调试拖拽传送实体
- **WHEN** 调试开关开启
- **AND** 客户端发送合法调试拖拽请求
- **THEN** 服务端将目标 entity 坐标设置为请求目标坐标
- **AND** 该拖拽不经过普通玩家移动权限和一步移动限制
- **AND** 服务端广播包含该 entity 最终坐标的 WorldDelta

#### Scenario: 调试删除移除实体
- **WHEN** 调试开关开启
- **AND** 客户端发送合法调试删除请求
- **THEN** 服务端从权威 GameWorld 移除目标 entity
- **AND** 服务端响应成功
- **AND** 服务端广播包含该 entity id 的 removed WorldDelta

#### Scenario: 调试编辑失败不广播
- **WHEN** 调试编辑请求因为参数非法、entity 不存在或开关关闭而失败
- **THEN** 服务端响应失败并返回 reason
- **AND** 权威 GameWorld 不产生状态变化
- **AND** 服务端不广播 WorldDelta
