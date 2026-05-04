## Context
当前 `add-authoritative-move-broadcast` 已经补了 observer 注册和 `G2C_EntityMovedNotify`，但它仍建立在“客户端自己提供 EntityId”的前提上。Unity demo 里 `ClientWorldDemo.localEntityId` 默认是 `1`，除非手动传 `--entityId=2`，否则多个客户端都会注册和移动 entity 1。

服务端 `AuthoritativeMoveWorldProvider` 现在预注册了 player 1 和 player 2，这只是静态测试数据，不是连接生命周期。`MoveObserverRegistry` 能记录 observer 和 owner，但它不是玩家分配系统，也不负责阻止 session A 操作 session B 的 entity。

Minestom 可借鉴的是边界形状：连接进入 server 后形成 Player 对象，Player 归属于 Instance/world，网络连接和实体身份绑定；world 内的 EntityTracker/同步系统只消费已经存在的 player/entity 关系。DG 这里不移植 Minestom 的继承体系和完整网络栈，只借鉴“连接身份先落到 world/entity 管理，再由移动和同步系统消费”的顺序。

## Goals
- 每个成功进入世界的 Fantasy session 都获得一个唯一 player entity。
- 服务端维护 session、player entity、当前坐标、observer 关系的统一入口。
- 客户端不再默认多个实例使用 entity 1。
- 移动请求只允许操作当前 session 绑定的 player entity。
- observer 广播能支持至少三个客户端同时在线，每个客户端代表不同玩家。
- 客户端 world 能同时存在本地玩家和远端玩家，并按服务端最终坐标更新。
- 验证拆分为协议生成、服务端规则、Unity EditMode、本地构建和手动端到端。

## Non-Goals
- 不实现账号、登录鉴权、角色选择或持久化。
- 不实现跨服、Roaming、Address 路由或房间匹配。
- 不实现 AOI、视野距离裁剪、进入/离开视野差量。
- 不实现客户端预测、回滚或插值。
- 不引入 Minestom 的 Java 实体继承树、ThreadDispatcher、Acquirable 或 packet flush 模型。
- 不做完整断线重连语义；第一版只需要清理已断开的 session，避免广播和分配状态污染。

## Decisions

### 用 MultiplayerEntityManager 作为第一版统一入口
服务端新增最小多人实体管理器，职责包含：
- 分配下一个可用 player entity id。
- 把 Fantasy session 绑定到 player entity。
- 把 player entity 注册到 `AuthoritativeMoveWorld`。
- 查询 session 当前绑定的 player entity。
- 断开或不可用 session 清理。
- 枚举在线 player entity snapshot，供新客户端初始化。

这个管理器是当前阶段的“统一管理器”。它不承载世界规则本身，移动合法性仍由 `AuthoritativeMoveWorld` 决定；它也不承载同步发送细节，广播仍由移动 Handler 或后续 SyncManager 触发。

### 进入世界协议先做最小 Join
新增 `C2G_JoinWorldRequest // IRequest,G2C_JoinWorldResponse`。第一版请求可以不带账号字段，响应包含：
- `Success`
- `EntityId`
- `CurrentX`
- `CurrentY`
- `Reason`

加入成功后，服务端分配 entity 并返回当前玩家初始坐标。后续如果需要把已有在线玩家 snapshot 一次性返回，可以增加 `G2C_WorldSnapshotNotify // IMessage` 或 `G2C_PlayerSpawnNotify // IMessage`，但第一版可以复用现有 moved notify 推一轮 snapshot，前提是 spec 和日志明确说明。

### 移动请求以绑定关系为准
`C2G_MoveRequest.EntityId` 可以暂时保留，便于兼容当前生成链路和日志，但 Handler 必须校验：
- session 未加入世界时拒绝。
- request.EntityId 不等于 session 绑定 entity 时拒绝。
- entity 不存在时拒绝。
- 合法时才进入 `AuthoritativeMoveWorld.Move(...)`。

这样可以直接阻断多个客户端默认操作 entity 1 的问题，也能防止客户端伪造移动别人的 entity。

### observer 注册从手动 entity id 转为 Join 副作用
第一版多人管理系统中，加入世界成功就意味着该 session 是共享世界 observer。当前 `C2G_RegisterMoveObserverRequest` 可以在实现阶段被保留为调试兼容入口，但主路径应该是 Join 成功后自动进入 observer 集合。这样 B 不需要先知道 A 的 entity id，也不需要靠移动请求懒注册。

### 客户端本地玩家由服务端分配结果创建
Unity demo 初始化时不应先创建 entity 1 再注册 observer。主路径应改为：
1. 等待 Fantasy session 可用。
2. 发送 JoinWorld。
3. 根据响应 entity id 创建或标记本地 player entity。
4. 把移动输入绑定到本地 player entity。
5. 收到其他 entity 的 snapshot/notify 时，通过 `ApplyServerMovementOrCreate` 创建或更新远端 player entity。

`--entityId` 可以保留为调试覆盖参数，但不应是多人主路径的身份来源。

### 三客户端作为明确验收线
两客户端只能证明“广播给另一个 session”。三客户端能证明管理器不是单一 owner 覆盖，也不是两个静态 id 的特殊情况。因此端到端手动验证必须包含至少三个客户端，服务端日志要能看到三个不同 session/entity 绑定，三个客户端画面或日志要能区分三个 entity。

## Risks / Trade-offs
- 第一版没有账号系统，同一用户多开会得到多个临时 player entity，这是预期行为。
- 如果只用 `Session` 做 dictionary key，断线清理需要在枚举或发送前剔除 disposed session；后续可接入 Fantasy 生命周期事件。
- 如果 snapshot 复用 moved notify，新客户端看到的“spawn”语义会不够清晰；实现阶段可以选择增加独立 spawn/snapshot notify，但需要保持协议字段最小。
- 当前 `AuthoritativeMoveWorld` 只校验阻挡和距离，不校验玩家占位冲突；如果三个玩家能走到同一格，应该在本变更任务中明确是否补“玩家占用格阻挡”。第一版建议补，避免多人验证时角色重叠。
- 当前 active change `add-authoritative-move-broadcast` 的第 9 节手动验证不应在多人管理完成前标为完成，否则会把“同一个 entity 被多个客户端观察”误判成“多玩家”。

## Migration Plan
1. 先实现协议和生成链路，新增 JoinWorld 主路径。
2. 再实现服务端管理器和规则验证。
3. 再调整移动 Handler 权限校验。
4. 再调整 Unity demo 初始化和本地玩家绑定。
5. 最后回到 `add-authoritative-move-broadcast` 的手动验证，用三客户端重新验证广播。

## Open Questions
- 初始坐标第一版是否按 entity id 自动错开，例如 `(0,0)`, `(0,1)`, `(0,2)`，还是先使用固定 spawn 点并由占位规则寻找附近空格？
- 是否现在就新增独立 `G2C_PlayerSpawnNotify`，还是先复用 `G2C_EntityMovedNotify` 做 snapshot？
