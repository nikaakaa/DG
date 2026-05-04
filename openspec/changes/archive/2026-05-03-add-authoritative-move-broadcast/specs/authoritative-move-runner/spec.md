## ADDED Requirements
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
服务端 SHALL 在 `C2G_MoveRequestHandler` 成功裁决移动后，回复发起者并向在线 observer 广播 `G2C_EntityMovedNotify`。

#### Scenario: 合法移动广播给发起者和其他客户端
- **WHEN** 客户端 A 发送合法 `C2G_MoveRequest`
- **AND** 服务端移动世界返回成功结果
- **THEN** 服务端回复 A 的 `G2C_MoveResponse Success=true`
- **AND** 服务端向在线 observer 集合广播 `G2C_EntityMovedNotify`
- **AND** A 与客户端 B 都能收到同一 entity 的最终坐标通知

#### Scenario: 先回复再广播
- **WHEN** 客户端 A 的合法移动被服务端接受
- **THEN** Handler 先发送 `G2C_MoveResponse`
- **AND** Handler 再发送 `G2C_EntityMovedNotify`

#### Scenario: 广播发生在服务端裁决之后
- **WHEN** Handler 准备广播 `G2C_EntityMovedNotify`
- **THEN** entity id 与最终坐标来自 `AuthoritativeMoveWorld` 的成功结果
- **AND** Handler 不绕过权威 world 自行决定移动结果

#### Scenario: 广播日志可验证
- **WHEN** 服务端发送 `G2C_EntityMovedNotify`
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
