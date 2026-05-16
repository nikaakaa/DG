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
服务端 SHALL 在权威 tick 中执行进入推动规则，并在推动产生状态变化或推力反馈后通过统一 WorldDelta 同步给在线客户端。当进入推动或 handoff 推动的目标是 connected body subject 时，服务端 MUST 在同一次成功移动结果中同步每个实际移动成员的最终状态和动画元数据。普通 push 链中间 subject 继续传递 push 但自身不移动时，服务端 MUST broadcast metadata-only WorldDelta so clients can play push feedback without changing authoritative coordinates.

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

#### Scenario: 连接体推动结果同步全体成员
- **WHEN** 玩家移动、机关推动或进入推动通过 handoff 成功推动一个 connected body
- **THEN** 服务端 `WorldDelta.ChangedEntities` MUST contain every moved member of that connected body
- **AND** each moved member snapshot MUST contain the authoritative final coordinate for the same server tick
- **AND** `WorldDelta.AnimationMetadata` MUST contain a movement metadata entry for every moved member
- **AND** observer clients MUST receive the same full-member `WorldDelta`

#### Scenario: 中间 subject 推力反馈同步
- **WHEN** a connected body or single entity receives push and continues handoff to a downstream subject
- **AND** that intermediate subject does not move during the current authoritative tick
- **THEN** 服务端 `WorldDelta.ChangedEntities` MUST NOT include that intermediate subject solely because it transmitted push
- **AND** `WorldDelta.AnimationMetadata` MUST contain mechanism push feedback metadata for the intermediate subject members
- **AND** observer clients MUST receive the metadata-only delta when no entity snapshot changed

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

### Requirement: 服务端权威 Runtime Effect 调试执行
服务端 SHALL execute debug runtime effect apply/remove through the same commit and effect spec boundary as ordinary effect application. Debug effect requests MUST resolve to an `EffectSpecId` and an `EffectSpec` from Luban-backed config or an explicit debug effect registry before producing `EffectApplication`. Debug execution MUST NOT construct ad hoc authoritative `EffectSpec` objects from `RuntimeEffectKind` as the main path.

#### Scenario: Debug apply resolves effect spec
- **WHEN** a client sends a debug apply runtime effect request using the legacy effect kind input
- **THEN** the server maps that input to an explicit effect spec id
- **AND** loads the effect definition from provider or debug registry
- **AND** creates an `EffectApplication` from that definition
- **AND** submits `AddRuntimeEffect` through commit

#### Scenario: Debug remove uses runtime source identity
- **WHEN** a client sends a debug remove runtime effect request with runtime effect id
- **THEN** the server submits `RemoveRuntimeEffect` through commit
- **AND** only the matching runtime effect source is removed
- **AND** final Component/tag state is recomputed by resolver

#### Scenario: Debug failure does not mutate world
- **WHEN** effect kind cannot resolve to an effect spec id, effect spec is missing, entity is missing, or debug editing is disabled
- **THEN** the server responds with failure reason
- **AND** no runtime effect source, final Component, final tag, or WorldDelta is produced

#### Scenario: Debug apply broadcasts final result
- **WHEN** debug apply runtime effect succeeds
- **THEN** the authoritative GameWorld changes only through commit and resolver
- **AND** observers receive the same final Component/tag result through WorldDelta

### Requirement: Runtime Effect Debug Verification
系统 SHALL verify server debug runtime effect behavior through server authoritative verification and manual two-client validation.

#### Scenario: Server verification covers debug effect
- **WHEN** server authoritative verification runs
- **THEN** it covers debug apply effect, debug remove effect, missing effect spec failure, and static source preservation
- **AND** it proves debug effect execution does not bypass commit

#### Scenario: Manual debug effect sync
- **WHEN** client A applies and removes runtime effects through debug UI
- **AND** client B observes the same server world
- **THEN** both clients converge to server final Component/tag state
- **AND** client B does not need runtime effect source data to display the result

### Requirement: Server Authoritative Source Layout Semantics
The Fantasy Hotfix authoritative move source layout SHALL separate network Handler entrypoints, application services, authoritative tick runtime, world bootstrap, synchronization, debug editing, and protocol mapping. Handler files MUST remain thin Fantasy message boundaries and MUST NOT become the place where gameplay rules, storage backend details, or client presentation policy are implemented.

#### Scenario: Handler remains an entrypoint
- **WHEN** a `C2G_*` request handler receives a client request
- **THEN** the handler validates session/request shape, converts protocol data to DG input data, calls an authoritative application service or runtime boundary, and replies
- **AND** it does not directly execute action arbitration, mutate storage adapter internals, construct Arch entities, or build Unity presentation state

#### Scenario: Runtime and sync are discoverable
- **WHEN** a developer needs to inspect server tick, input queue, world delta construction, observer enumeration, or broadcast behavior
- **THEN** those files are located under directories whose names identify runtime or sync responsibilities
- **AND** they are not hidden inside generic infrastructure or Handler folders

#### Scenario: Fantasy conventions survive migration
- **WHEN** server files are moved into the new layout
- **THEN** Fantasy message handlers still use source-generator-compatible classes
- **AND** generated `.g.cs` files are not manually edited
- **AND** async Fantasy code continues to use `FTask` where async behavior is required

### Requirement: Server Layout Migration Verification
The server layout migration SHALL prove that authoritative behavior, session observer behavior, and WorldDelta broadcast behavior remain unchanged by directory and file movement.

#### Scenario: Server verification passes
- **WHEN** the server authoritative verification project runs after migration
- **THEN** player movement, blocked movement, debug spawn, debug move, debug remove, runtime effect debug, observer registration, and WorldDelta sync scenarios still pass
- **AND** failures identify behavior regressions rather than missing file paths

#### Scenario: Manual two-client sync still works
- **WHEN** the user manually starts the server and two Unity clients after migration
- **AND** client A moves, builds, drags, deletes, or applies an approved debug runtime effect
- **THEN** client B observes the server-authoritative final state through snapshot/delta
- **AND** no server behavior depends on the old physical source directory

### Requirement: 服务端权威拍点输入缓冲
服务端 SHALL 在普通玩家输入进入 action pipeline 前，按 `entityId + beatTick` 收口为该实体该拍的最终输入。普通玩家输入缓冲 MUST NOT 裁决阻挡、推动、机关、connected body 或 runtime effect 结果。

#### Scenario: 同实体同拍后输入覆盖
- **WHEN** 同一 entity 在同一 beatTick 提交多个普通玩家输入
- **THEN** 服务端只保留最后一次输入作为该拍最终输入
- **AND** 被覆盖输入完成为 Replaced
- **AND** 被覆盖输入不进入 action queue

#### Scenario: 不同实体同拍互不覆盖
- **WHEN** entity A 和 entity B 在同一 beatTick 提交普通玩家输入
- **THEN** 服务端分别保留 A 和 B 的最终输入
- **AND** A 的后续输入不会替换 B 的输入

#### Scenario: 当前拍只消费一次
- **WHEN** 服务端 drain 某个 beatTick 的普通玩家输入
- **THEN** 返回该拍每个 entity 的最终输入
- **AND** 第二次 drain 同一 beatTick 不再返回已消费输入

#### Scenario: 未来拍不提前消费
- **WHEN** 服务端当前 tick 是 N
- **AND** 缓冲中存在 beatTick 大于 N 的普通玩家输入
- **THEN** 当前 tick 不消费该输入
- **AND** 该输入不会提前进入 action queue

#### Scenario: 过期输入拒绝
- **WHEN** 普通玩家输入的 beatTick 早于服务端可接受窗口
- **THEN** 服务端完成该输入为 Expired 或 Rejected
- **AND** 该输入不进入 action queue

### Requirement: 玩家输入意图进入现有行为管线
服务端 SHALL 将已消费的普通玩家输入转换为现有 player movement action，并继续通过 `ActionSpec -> ActionRequest -> Strategy -> Arbitration -> Planning -> Commit -> WorldDelta` 管线裁决。普通玩家输入 MUST 表达方向或动作意图，服务端 MUST 使用权威当前位置推导移动目标格。

#### Scenario: 方向意图转成服务端目标格
- **WHEN** 服务端消费一个 direction 为 Right 的玩家输入
- **AND** 权威 GameWorld 中该 entity 当前坐标是 `(1,2)`
- **THEN** 服务端生成的 player movement action 目标格是 `(2,2)`
- **AND** 目标格不是直接信任客户端提交的表现坐标

#### Scenario: 行为裁决仍由 action pipeline 完成
- **WHEN** 已消费玩家输入对应目标格被阻挡或触发推动
- **THEN** 服务端通过现有 action pipeline 得到成功、失败、推动或反馈结果
- **AND** 输入缓冲层不直接修改 GameWorld 坐标

#### Scenario: WorldDelta 仍是最终同步结果
- **WHEN** 玩家输入经 action pipeline 产生坐标、组件、tag、生成或删除变化
- **THEN** 服务端通过统一 WorldDelta 同步最终权威结果
- **AND** 输入响应不替代 WorldDelta

#### Scenario: 调试编辑不走普通输入缓冲
- **WHEN** 客户端发送 debug spawn、debug drag move、debug remove 或 debug runtime effect 请求
- **THEN** 服务端按调试编辑路径处理
- **AND** 这些请求不被 `entityId + beatTick` 普通玩家输入缓冲覆盖

### Requirement: 普通输入兼容迁移
服务端 MAY 保留旧 `C2G_MoveRequest` 作为迁移期兼容入口，但旧入口 MUST 适配到同一拍点输入缓冲。普通移动请求 MUST NOT 继续以 FIFO 多请求形式直接进入 player movement action queue。

#### Scenario: 旧移动请求进入拍点缓冲
- **WHEN** 客户端通过旧移动请求提交普通移动
- **THEN** 服务端把该请求适配为 direction/beat 输入
- **AND** 该输入参与同一实体同一拍覆盖规则

#### Scenario: 旧目标格不作为权威裁决来源
- **WHEN** 旧移动请求包含 TargetX 和 TargetY
- **THEN** 服务端可以用它们推导方向或校验相邻移动
- **AND** 最终 player movement action 的目标格来自服务端权威当前位置和方向

#### Scenario: FIFO 多移动被禁止
- **WHEN** 同一 entity 在同一 beatTick 通过旧移动请求提交多个方向
- **THEN** 服务端只生成一个 player movement action
- **AND** 最终 action 对应最后一次输入

