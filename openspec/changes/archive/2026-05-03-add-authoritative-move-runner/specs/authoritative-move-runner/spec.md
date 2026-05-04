## ADDED Requirements

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
服务端 SHALL 维护独立于 Unity 客户端的最小移动世界状态，用于裁决玩家是否可以进入目标坐标。

#### Scenario: 注册玩家坐标
- **WHEN** 服务端最小世界接收一个玩家 id 和初始坐标
- **THEN** 后续可以通过玩家 id 读取该玩家当前坐标

#### Scenario: 合法移动更新坐标
- **WHEN** 玩家请求移动到可进入坐标
- **THEN** 服务端移动世界更新该玩家坐标
- **AND** 返回成功结果和最终坐标

#### Scenario: 阻挡格拒绝移动
- **WHEN** 玩家请求移动到阻挡坐标
- **THEN** 服务端移动世界拒绝该移动
- **AND** 玩家坐标保持为移动前坐标
- **AND** 返回失败结果、最终坐标和业务错误原因

#### Scenario: 服务端不引用 Unity World
- **WHEN** 实现服务端最小移动世界
- **THEN** 它不依赖 UnityEngine、MonoBehaviour 或客户端 `DG.Map.World`

### Requirement: 移动 RPC Handler
服务端 SHALL 使用 Fantasy RPC Handler 接收 `C2G_MoveRequest`，调用服务端移动世界，并返回 `G2C_MoveResponse`。

#### Scenario: Handler 处理合法移动
- **WHEN** 客户端发送合法 `C2G_MoveRequest`
- **THEN** Handler 调用服务端移动世界
- **AND** 响应 `Success=true`
- **AND** 响应包含玩家 id、最终坐标和原始 `ClientTick`

#### Scenario: Handler 处理非法移动
- **WHEN** 客户端发送目标不可进入的 `C2G_MoveRequest`
- **THEN** Handler 调用服务端移动世界
- **AND** 响应 `Success=false`
- **AND** 响应包含当前最终坐标、`MoveErrorCode`、`Reason` 和原始 `ClientTick`

#### Scenario: Handler 日志可验证
- **WHEN** Handler 收到移动请求
- **THEN** 服务端日志记录请求玩家、目标坐标和处理结果

### Requirement: 服务端推进器验证
服务端 SHALL 提供可重复的构建和规则验证方式，证明合法移动被接受且非法移动被拒绝。

#### Scenario: 服务端构建通过
- **WHEN** 运行 `dotnet build Server/Server.sln -v minimal`
- **THEN** 服务端解决方案构建成功

#### Scenario: 最小规则验证通过
- **WHEN** 运行服务端移动世界的最小验证
- **THEN** 合法移动成功用例通过
- **AND** 阻挡格失败用例通过

