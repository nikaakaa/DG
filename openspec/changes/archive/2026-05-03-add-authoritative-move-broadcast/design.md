## Context
当前已完成的移动链路是单客户端闭环：Unity 输入通过 `ClientMoveNetworkSubmitter` 调用生成的 `session.C2G_MoveRequest(...)`，服务端 `C2G_MoveRequestHandler` 调用 `AuthoritativeMoveWorldProvider.World.Move(...)` 裁决，再用 `G2C_MoveResponse` 返回发起者，客户端按响应最终坐标应用到本地 `ClientWorldRunner`。

现有服务端没有在线 observer/session 集合。`C2G_MoveRequestHandler` 当前只知道请求发起者的 `Session` 和请求中的 `EntityId`，没有机制把成功移动结果推给其他客户端。现有协议也没有服务端主动推送 entity 移动结果的 `IMessage`。

Minestom 的可借鉴点不是 Minecraft 规则，而是阶段边界：`ServerProcessImpl` 先处理 scheduler/connection，再 tick instance/chunks/entities，tick end 后统一 flush；`Instance` 是 world facade，聚合 chunks、entities、scheduler、tracker；`EntityTrackerImpl` 按 Target 和 chunk 维护索引，移动时产生 add/remove 式可见性差量；`Batch` 先收集变更，再统一 apply/通知。当前切片只把这些压缩成“权威 world 裁决成功 -> 收集一条 moved notify -> 面向在线 observer/session 集合发送”。

## Goals
- 保持 Fantasy 是网络 shell，服务端 `AuthoritativeMoveWorld` 是移动裁决核心。
- 用最小在线 observer/session 集合支撑双客户端移动同步。
- 让只连接但尚未移动过的客户端也能显式进入 observer 集合。
- 成功移动时让发起者和其他在线客户端收到同一 entity 的最终坐标。
- 失败移动不广播，避免其他客户端被非法意图污染。
- 客户端 push handler 复用现有 world/runner 应用边界，不引入预测、回滚或插值。
- 每个实现步骤都有构建、测试或运行时日志验证方式。

## Non-Goals
- 不实现 AOI、chunk view distance、按距离可见性、进入/离开视野差量。
- 不实现账号系统、登录鉴权、复杂房间、跨服路由、Roaming 或 Address 转发。
- 不改变现有 `C2G_MoveRequest` / `G2C_MoveResponse` RPC 语义。
- 不把 Minestom 的 Java 网络协议、packet flush、ThreadDispatcher、Acquirable 或完整事件总线搬到本项目。
- 不在 Unity 客户端创建独立 tick 线程。

## Decisions

### 协议新增单向通知
新增 `G2C_EntityMovedNotify // IMessage`，字段保持最小：`EntityId`、`FinalX`、`FinalY`、`ServerTick` 或等价服务端序号、`ClientTick`。`ClientTick` 用于关联发起者原始输入，`ServerTick` 第一版可为单调递增服务端移动序号，避免把通知伪装成客户端本地 tick。

### 在线集合先按 entity/session 显式维护
第一版服务端增加最小在线 observer/session 集合，并单独保留 entity owner 归属记录。observer 集合负责“谁会收到广播”，entity owner 负责“哪个 session 正在提交哪个 entity 的移动”。不要把广播集合做成单一 `Dictionary<EntityId, Session>`，否则 B 只观察 A 的 entity 时会被覆盖或遗漏。

当前没有账号系统，所以第一版通过显式注册请求或 demo 初始化请求把客户端 session 加入 observer 集合，并可附带本客户端关注或拥有的 entity id。移动请求也可以刷新 entity owner，但不能作为 B 进入 observer 集合的唯一方式。

### Handler 保持网络入口薄层
`C2G_MoveRequestHandler` 继续只负责读取请求、调用权威 world、填充响应、记录日志和触发广播。移动是否成功由 `AuthoritativeMoveWorld` 决定；广播内容来自 `MoveResult` 的最终坐标，不从客户端目标坐标直接构造。

### 广播边界先是全在线 observer 集合
本阶段 observer 就是显式注册过的在线 session 集合，不做 AOI。成功移动后先完成 `G2C_MoveResponse` 回复，再向集合中未释放的 session 发送 `G2C_EntityMovedNotify`。广播包含发起者，让 A 和 B 都通过同一种 notify 路径收敛到同一坐标；A 仍保留 RPC response 作为请求完成与失败原因来源。

### 客户端 push handler 只应用最终坐标
Unity 客户端新增 `G2C_EntityMovedNotifyHandler`，通过现有 `ClientWorldRunner` 或集中接入点应用 `EntityId` 的最终坐标。handler 不直接生成预测结果，不执行移动规则，不新建第二个 world。

### 验证分层
验证必须拆开报告：OpenSpec validate、协议导出成功、服务端构建成功、Unity 编译/EditMode 测试成功、服务端 Gate KCP 20000 启动、双客户端合法移动 B 收到 notify 并更新、非法移动 B 未更新。不能把其中任一步通过说成完整完成。

## Risks / Trade-offs
- 当前没有连接生命周期 hook 的事实证据，在线集合可能先在移动请求时懒注册，并在发送时剔除 disposed session；这能满足最小切片，但不是最终登录/断线模型。
- 如果只靠移动请求懒注册 observer，B 在未移动前会收不到 A 的广播；因此本 proposal 要求新增显式 observer 注册路径。
- A 同时收到 RPC response 和 notify，客户端应用需要幂等，重复应用同一最终坐标不能导致错误 dirty 状态或日志误判。
- 全在线广播会把所有在线客户端都当 observer，适合双客户端验证，不适合大地图规模；AOI 后续单独 proposal。
- 如果 Unity handler 难以直接找到当前 `ClientWorldRunner`，应先增加一个最小集中访问边界，而不是让 handler 创建或持有新 world。
- Fantasy `Session` 可能不适合在纯规则验证程序里直接构造；服务端集合规则应抽出可测试的纯数据边界，真实 `Session.Send(...)` 通过构建和运行时日志验证。

## Open Questions
- 双客户端验证时是否使用两个 Unity Editor/Build 实例，还是一个 Editor 加一个 Player build；实现阶段需要按实际可运行方式写清命令和日志观察点。
