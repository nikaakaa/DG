## Context
现有链路已经具备 JoinWorld、移动 RPC、observer 集合、`G2C_EntityMovedNotify` 广播和 Unity 客户端应用服务端坐标的雏形。这个 change 不再继续围绕玩家输入证明同步，而是增加一个服务端自己推进的无限 tick 演示：只要服务端 world 活着，球就持续移动并广播权威状态。

本提案遵守当前架构命名：
- `Fantasy Handler` 只做网络入口和 session/observer 注册。
- `System` 负责游戏规则、tick 推进和同步批处理。
- `Command` 表示外部意图。本 demo 的运动不需要客户端 Command。
- `Event` 表示已发生事实，可用于后续拆分反弹、spawn、despawn 等响应。
- `Component/Tag` 描述实体，`Dirty` 记录 world 已变化内容。

## Goals
- 做出一个能长期运行的服务端固定 tick 场景。
- 用配置实例化出来的两个 entity 证明服务端权威状态同步。
- 两个 Unity 客户端同时连接后，看到同一 tick 序列下的同一球位置和边界。
- 第一版只证明链路，不引入客户端预测、插值、回滚、完整 AOI 或复杂物理。

## Non-Goals
- 不实现通用物理引擎。
- 不实现 rollback、客户端预测或插值。
- 不实现完整 UE GameplayTag 或 GameplayAbility。
- 不把反弹规则写进 Fantasy Handler。
- 不修改生成的 `.g.cs` 文件。
- 不把“球”“边界”做成 entity 的硬编码名字。

## Decisions

### Decision: 两个 demo entity 由配置实例化
entity 本身不应该有业务名字。第一版 demo 使用配置声明两个 archetype：

- 一个运动体 archetype，配置包含初始位置、速度方向、可反弹 tag 或等价能力标记。
- 一个边界体 archetype，配置包含边界范围、边界 tag 或等价能力标记。

运行时只创建两个 entity id，并把配置里的组件和 tag 附到 entity 上。System 通过组件/tag 查询参与反弹规则的实体，而不是通过 entity 名字查找。

如果后续要改成两个球，只需要在配置中增加第二个运动体实例；如果边界不再需要同步，也可以把边界体配置降级为 world 配置。

### Decision: 无限 Tick 由服务端 world 生命周期拥有
无限 tick 指服务端启动 demo world 后按固定间隔持续推进，直到 world 或服务端关闭。它不是 Unity `Update` 驱动的本地动画，也不是客户端发包触发的移动。

服务端实现时需要防止重复启动多个 tick loop；关闭或释放 world 时必须能停止 tick loop。

### Decision: Handler 不持有游戏规则
网络 Handler 可以处理 Join/Observe/StartDemo 之类入口，把 session 加入 observer 集合并返回当前 snapshot。Handler 不计算运动体的位置，不判断反弹，不直接越过 world 改写 demo 状态。

反弹由 `BouncingDemoTickSystem` 或等价 System 执行；同步由 `DemoSyncSystem` 或等价同步边界执行。

### Decision: 协议优先
如果现有 `G2C_EntityMovedNotify` 字段足够表达运动体的整数坐标，第一版可以复用它作为位置通知，但仍需要明确 demo entity 的 `ConfigId`/`ArchetypeId`/tag 和 snapshot 入口。若需要同步边界范围、速度或 tag，应先更新 `Tools/NetworkProtocol/Outer/OuterMessage.proto`，再通过协议导出生成服务端和客户端代码。

不得手改 `Server/Entity/Generate/NetworkProtocol` 或 `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol` 下的生成文件。

### Decision: 客户端只应用服务端状态
Unity 客户端显示运动体，但不本地计算权威位置。客户端可以把收到的服务端 snapshot/delta 应用到当前 `World`，再由渲染层根据配置或 tag 选择视图。第一版不做预测和插值，避免把“同步闭环”与“视觉平滑”混在一起。

## Risks / Trade-offs
- 风险：每 tick 广播可能产生较高日志或网络噪声。
  Mitigation: 第一版允许低频固定 tick 或仅在状态变化时广播，并在验收里检查 tick 序号连续性。
- 风险：复用移动通知会让 player movement 和 demo movement 语义混杂。
  Mitigation: 如果字段不足或调试混乱，新增 demo snapshot/delta 协议，保持语义清楚。
- 风险：客户端场景只看到动画但无法证明网络同步。
  Mitigation: 手动验收必须同时检查两个客户端日志中的 server tick/entity 坐标一致。

## Validation
- OpenSpec 严格校验通过：`openspec validate add-bouncing-ball-tick-demo --strict --no-interactive`。
- Unity TestFramework 验证客户端应用 snapshot/delta、两个实体创建或更新、重复 tick 幂等。
- 服务端验证项目或测试验证反弹序列、边界反转、重复启动保护、observer 广播目标。
- 手动端到端验收：启动服务端，启动两个 Unity 客户端，两个客户端 Join/Observe 同一 demo world，球持续反弹，两个客户端显示与日志中的 server tick/坐标一致。

## Open Questions
- 第一版是否必须新增独立 demo snapshot/delta 协议，还是允许复用 `G2C_EntityMovedNotify` 并补充 `ConfigId`/`ArchetypeId`/tag 字段？
- 边界体是否必须在客户端可视化为边框，还是只要作为同步实体存在并能在调试面板中看到即可？
