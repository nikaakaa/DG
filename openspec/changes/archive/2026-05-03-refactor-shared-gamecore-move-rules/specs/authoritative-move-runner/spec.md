## MODIFIED Requirements
### Requirement: 服务端最小移动世界
服务端 SHALL 维护独立于 Unity 客户端的权威 GameWorld 状态，用于裁决玩家和自动移动实体是否可以进入目标坐标。该权威 GameWorld SHALL 基于共享 GameCore 的实体、组件、坐标、MoveCommand 和 MovementResolveSystem，而不是只维护玩家 id 到坐标的专用字典。

#### Scenario: 注册玩家坐标
- **WHEN** 服务端最小世界接收一个玩家 id 和初始坐标
- **THEN** 后续可以通过玩家 id 读取该玩家当前坐标
- **AND** 该玩家在权威 GameWorld 中表现为拥有玩家相关组件的 entity

#### Scenario: 合法移动更新坐标
- **WHEN** 玩家请求移动到可进入坐标
- **THEN** 服务端移动世界把请求转换为 MoveCommand
- **AND** MovementResolveSystem 更新该玩家坐标
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
服务端 SHALL 使用 Fantasy RPC Handler 接收 `C2G_MoveRequest`，验证 session 与 player entity 绑定，然后提交玩家输入 MoveCommand 给服务端权威 GameWorld，并返回 `G2C_MoveResponse`。

#### Scenario: Handler 处理合法移动
- **WHEN** 客户端发送合法 `C2G_MoveRequest`
- **THEN** Handler 验证该 session 可以移动该 player entity
- **AND** Handler 提交玩家输入 MoveCommand
- **AND** 响应 `Success=true`
- **AND** 响应包含玩家 id、最终坐标和原始 `ClientTick`

#### Scenario: Handler 处理非法移动
- **WHEN** 客户端发送目标不可进入的 `C2G_MoveRequest`
- **THEN** Handler 提交玩家输入 MoveCommand 并接收失败结果
- **AND** 响应 `Success=false`
- **AND** 响应包含当前最终坐标、`MoveErrorCode`、`Reason` 和原始 `ClientTick`

#### Scenario: Handler 日志可验证
- **WHEN** Handler 收到移动请求
- **THEN** 服务端日志记录请求玩家、目标坐标、MoveCommand 来源和处理结果

### Requirement: 成功移动广播
服务端 SHALL 在玩家 MoveCommand 成功裁决后，回复发起者并向在线 observer 广播统一 WorldDelta。

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
