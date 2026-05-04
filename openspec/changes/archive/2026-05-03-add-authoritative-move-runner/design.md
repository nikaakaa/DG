## Context
当前客户端地图运行时已经形成了本地推进结构：`ClientWorldDemo` 收集按键，调用 `ClientWorldRunner.SubmitMovement`，`MovementSystem` 在固定 tick 中消费命令并直接调用 `World.MoveEntity`。这条链路适合本地最小验证，但还不是服务端权威链路。

当前协议源位于 `Tools/NetworkProtocol`，导出工具位于 `Tools/ProtocolExportTool`。移动协议已经可以作为 proposal 的目标形状：`C2G_MoveRequest` 携带 `EntityId`、目标坐标和 `ClientTick`，`G2C_MoveResponse` 返回成功状态、最终坐标、业务错误和原始 tick。

服务端是 Fantasy 三项目结构：`Main` 启动进程，`Entity` 存放共享实体、配置和生成协议，`Hotfix` 存放 Handler 与业务逻辑。Handler 应放在 Hotfix assembly，由 Fantasy source generator 注册；协议生成文件不能手改。

## Goals
- 建立一条最小服务端权威移动闭环：客户端输入意图 -> 协议请求 -> 服务端推进器裁决 -> 响应最终坐标 -> 客户端应用结果。
- 服务端拥有自己的最小世界状态，不引用 Unity 客户端 `World`。
- 客户端继续复用现有 `ClientWorldRunner` 和可视化路径，只改变移动提交的权威来源。
- 每一步都能独立验证，先通过协议导出和构建，再验证请求响应和坐标更新。

## Non-Goals
- 不实现客户端预测、回滚或插值。
- 不实现多玩家广播、AOI 可见性同步或世界 delta 推送。
- 不实现复杂地图加载、寻路、战斗、碰撞体积或 ECS/DOTS 服务端迁移。
- 不把 Unity `World`、`Vector2Int` 或 MonoBehaviour 引入服务端。
- 不手动修改生成协议文件或 source generator 产物。

## Decisions

### 服务端推进器边界
服务端新增最小推进器，维护 `playerId -> coord` 和阻挡格。它提供 `CanEnter` 与 `Move`，返回最终坐标和错误原因。Handler 不直接操作字典细节，避免把世界规则散落在网络入口里。

### 客户端应用边界
客户端按键后不再立即让本地 `MovementSystem` 裁决移动。第一阶段推荐新增一个网络移动提交边界，收到成功响应后再把最终坐标提交给现有 runner 或应用到本地 world。失败响应保持原坐标，并输出可验证日志。

### 协议与错误码
`IResponse` 自带框架级 `ErrorCode`，业务移动失败使用 `MoveErrorCode` 和 `Reason`。框架错误表示请求处理失败；业务失败表示请求被服务端正常处理但移动被规则拒绝。

### 验证顺序
实现必须先验证协议导出，再验证服务端构建，再验证 Handler 命中，最后验证 Unity 客户端响应驱动移动。不能把“生成成功”“服务端构建通过”“Gate 启动成功”“客户端坐标更新”混成一个状态。

## Risks
- 当前客户端项目未确认 Fantasy.Unity 包、`FANTASY_UNITY` 宏和运行时连接组件是否完整；实现阶段必须先做客户端网络依赖检查。
- 当前 `Server/Main/NLog.cs` 可能存在 Fantasy `ILog` 接口不匹配的构建闸门；实现阶段需要先让 `Server.sln` 可构建，再添加 Handler。
- `EntityId` 类型在协议里是 `int64`，客户端现有 `Entity.Id` 是本地整数语义；实现阶段要明确本地实体 id 与服务端玩家 id 的映射，第一版可以使用同一数值但要集中在接入层。

