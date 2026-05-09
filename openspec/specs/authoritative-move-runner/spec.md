# authoritative-move-runner Specification

## Purpose
TBD - created by archiving change add-authoritative-move-runner. Update Purpose after archive.
## Requirements
### Requirement: 协议导出驱动移动消息
系统 SHALL 通过协议源文件和导出工具生成移动请求与响应代码，而不是手写服务端或客户端生成文件。

#### Scenario: 生成服务端移动协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端生成目录包含 `C2G_MoveRequest` 和 `G2C_MoveResponse`
- **AND** 生成的 request 实现 `IRequest`
- **AND** 生成的 response 实现 `IResponse`

#### Scenario: 生成客户端移动调用 helper
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 客户端生成目录包含 `NetworkProtocolHelper.C2G_MoveRequest(...)`
- **AND** 该 helper 通过 `Session.Call` 发送 RPC 请求

#### Scenario: 业务错误码不覆盖框架错误码
- **WHEN** `G2C_MoveResponse` 被导出为 C# 类型
- **THEN** 框架级错误使用 `IResponse.ErrorCode`
- **AND** 移动规则失败原因使用独立的 `MoveErrorCode` 和 `Reason`

### Requirement: 服务端最小移动世界
服务端 SHALL 维护独立于 Unity 客户端的权威 GameWorld 状态，用于裁决玩家和自动移动实体是否可以进入目标坐标。该权威 GameWorld SHALL 基于共享 GameCore 的实体、组件、坐标、WorldAction、ActionSpec、ActionRequest 和 state-driven rule system，而不是只维护玩家 id 到坐标的专用字典。

#### Scenario: 注册玩家坐标
- **WHEN** 服务端最小世界接收一个玩家 id 和初始坐标
- **THEN** 后续可以通过玩家 id 读取该玩家当前坐标
- **AND** 该玩家在权威 GameWorld 中表现为拥有玩家相关组件的 entity

#### Scenario: 合法移动更新坐标
- **WHEN** 玩家请求移动到可进入坐标
- **THEN** 服务端移动世界把请求转换为玩家 movement action
- **AND** state-driven rule system 通过 action / intent / plan / commit 管线更新该玩家坐标
- **AND** 返回成功结果和最终坐标

#### Scenario: 阻挡格拒绝移动
- **WHEN** 玩家请求移动到阻挡坐标
- **THEN** 服务端移动世界拒绝该移动
- **AND** 玩家坐标保持为移动前坐标
- **AND** 返回失败结果、最终坐标和业务错误原因

#### Scenario: 服务端不引用 Unity World
- **WHEN** 实现服务端最小移动世界
- **THEN** 它不依赖 UnityEngine、MonoBehaviour 或客户端 `DG.Map.World`
- **AND** 它可以引用 Shared GameCore

### Requirement: 移动 RPC Handler
服务端 SHALL 使用 Fantasy RPC Handler 接收 `C2G_MoveRequest`，验证 session 与 player entity 绑定，然后提交玩家 movement action 给服务端权威 GameWorld，并返回 `G2C_MoveResponse`。

#### Scenario: Handler 处理合法移动
- **WHEN** 客户端发送合法 `C2G_MoveRequest`
- **THEN** Handler 验证该 session 可以移动该 player entity
- **AND** Handler 提交玩家 movement action
- **AND** 响应 `Success=true`
- **AND** 响应包含玩家 id、最终坐标和原始 `ClientTick`

#### Scenario: Handler 处理非法移动
- **WHEN** 客户端发送目标不可进入的 `C2G_MoveRequest`
- **THEN** Handler 提交玩家 movement action 并接收失败结果
- **AND** 响应 `Success=false`
- **AND** 响应包含当前最终坐标、`MoveErrorCode`、`Reason` 和原始 `ClientTick`

#### Scenario: Handler 日志可验证
- **WHEN** Handler 收到移动请求
- **THEN** 服务端日志记录请求玩家、目标坐标、movement action 来源和处理结果

### Requirement: 服务端推进器验证
服务端 SHALL 提供可重复的构建和规则验证方式，证明合法移动被接受且非法移动被拒绝。

#### Scenario: 服务端构建通过
- **WHEN** 运行 `dotnet build Server/Server.sln -v minimal`
- **THEN** 服务端解决方案构建成功

#### Scenario: 最小规则验证通过
- **WHEN** 运行服务端移动世界的最小验证
- **THEN** 合法移动成功用例通过
- **AND** 阻挡格失败用例通过

### Requirement: 移动结果通知协议
系统 SHALL 通过 Outer 协议源和导出工具生成 observer 注册消息与 `G2C_EntityMovedNotify` 单向通知，用于让客户端进入最小广播集合，并接收服务端主动推送的成功移动结果。

#### Scenario: 生成 observer 注册协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端生成目录包含 observer 注册 request/response
- **AND** 客户端生成目录包含 observer 注册 helper
- **AND** 注册 request 实现 `IRequest`
- **AND** 注册 response 实现 `IResponse`

#### Scenario: 生成服务端移动通知协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 服务端生成目录包含 `G2C_EntityMovedNotify`
- **AND** 生成的 notify 实现 `IMessage`

#### Scenario: 生成客户端移动通知协议
- **WHEN** 在 `Tools/ProtocolExportTool` 运行协议导出
- **THEN** 客户端生成目录包含 `G2C_EntityMovedNotify`
- **AND** 客户端可以为该消息实现 Fantasy push handler

#### Scenario: 通知只表达服务端最终结果
- **WHEN** 服务端构造 `G2C_EntityMovedNotify`
- **THEN** 通知包含 entity id、服务端最终坐标、服务端移动序号和原始 `ClientTick`
- **AND** 通知坐标来自服务端裁决结果，而不是直接来自客户端请求目标坐标

### Requirement: 最小在线移动观察者集合
服务端 SHALL 维护最小在线 observer/session 集合，并将 observer 集合与 entity owner 归属记录分离，用于在没有 AOI 的第一版中确定移动结果通知的接收者。

#### Scenario: 注册在线玩家 session
- **WHEN** 服务端收到 observer 注册请求
- **THEN** 服务端将当前 Fantasy `Session` 加入 observer 集合
- **AND** 服务端可以记录该 session 关联的 entity id

#### Scenario: 移动请求刷新 owner 但不替代 observer 注册
- **WHEN** 服务端收到带有 entity id 的移动请求
- **THEN** 服务端可以刷新该 entity id 的 owner session
- **AND** 该刷新不移除其他已注册 observer

#### Scenario: 枚举在线 observer
- **WHEN** 服务端需要广播一次成功移动结果
- **THEN** 服务端可以枚举当前在线且未释放的 session
- **AND** 本阶段所有在线 session 都被视为 observer

#### Scenario: 未移动客户端也能观察
- **WHEN** 客户端 B 已完成 observer 注册
- **AND** B 尚未发送任何移动请求
- **THEN** A 的成功移动广播仍会包含 B

#### Scenario: 清理不可用 session
- **WHEN** 在线集合中存在已释放 session
- **THEN** 广播时不向该 session 发送通知
- **AND** 该 session 不阻塞其他 observer 收到通知

### Requirement: 成功移动广播
服务端 SHALL 在玩家 movement action 成功裁决后，回复发起者并向在线 observer 广播统一 WorldDelta。

#### Scenario: 合法移动广播给发起者和其他客户端
- **WHEN** 客户端 A 发送合法 `C2G_MoveRequest`
- **AND** 服务端权威 GameWorld 返回成功结果
- **THEN** 服务端回复 A 的 `G2C_MoveResponse Success=true`
- **AND** 服务端向在线 observer 集合广播包含该 entity 最终状态的 WorldDelta
- **AND** A 与客户端 B 都能收到同一 entity 的最终坐标

#### Scenario: 先回复再广播
- **WHEN** 客户端 A 的合法移动被服务端接受
- **THEN** Handler 先发送 `G2C_MoveResponse`
- **AND** Handler 再触发统一同步广播

#### Scenario: 广播发生在服务端裁决之后
- **WHEN** Handler 准备广播移动结果
- **THEN** entity id 与最终状态来自服务端权威 GameWorld 的裁决结果
- **AND** Handler 不绕过权威 GameWorld 自行决定移动结果

#### Scenario: 广播日志可验证
- **WHEN** 服务端发送移动结果同步
- **THEN** 服务端日志记录 entity id、最终坐标、observer 数量和原始 `ClientTick`

### Requirement: 非法移动不广播
服务端 SHALL 对非法移动只回复发起者失败结果，不向其他客户端广播移动通知。

#### Scenario: 阻挡格移动只回复失败
- **WHEN** 客户端 A 请求移动到阻挡格
- **THEN** 服务端回复 A 的 `G2C_MoveResponse Success=false`
- **AND** 响应包含最终坐标、`MoveErrorCode`、`Reason` 和原始 `ClientTick`
- **AND** 服务端不发送 `G2C_EntityMovedNotify`

#### Scenario: 非法移动日志可验证
- **WHEN** 服务端拒绝一次移动
- **THEN** 服务端日志记录失败原因
- **AND** 服务端日志明确记录本次移动未广播

### Requirement: 服务端进入推动 tick
服务端 SHALL 在权威 tick 中执行进入推动规则，并在推动产生状态变化后通过统一 WorldDelta 同步给在线客户端。

#### Scenario: 推动系统参与服务端 tick
- **WHEN** 服务端 tick 推进
- **THEN** 服务端先把自动移动生成 state-driven auto movement action 或 intent
- **AND** 服务端再把进入推动生成 state-driven mechanism push action 或 intent
- **AND** 服务端最后广播包含推动结果的 WorldDelta

#### Scenario: 推动结果同步给观察者
- **WHEN** state-driven mechanism push 成功推动一个 player entity
- **THEN** 服务端 GameWorld 标记该 entity dirty
- **AND** 在线 observer 收到包含该 entity 最终坐标的 WorldDelta
- **AND** 客户端不通过本地传送带规则推导权威坐标

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
- **AND** 该请求语义为调试传送而不是普通 movement action

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


