## 1. Proposal validation

- [x] 1.1 确认本 change 只覆盖同 tick push vector composition，不处理客户端动画、能量衰减、多格强推或同 tick 全链求解。
- [x] 1.2 对照 `refactor-action-output-cleanup`，确认本 change 不修改 PushOnEnter 配置输出和 pending 主链路隔离语义。
- [x] 1.3 运行 `openspec validate add-push-vector-composition --strict --no-interactive`。

## 2. Push vector model

- [x] 2.1 定义 push contribution 数据结构，包含 subject key、direction、ready tick、spec id、causality 和 contribution count。
- [x] 2.2 定义 net vector 计算，支持 Up/Down/Left/Right 抵消和累加。
- [x] 2.3 定义 metadata，保留 total contribution、per-direction contribution、net vector、energy placeholder 和 causality samples。
- [x] 2.4 确认 metadata 不改变当前移动距离、优先级、cost 或强度。
- [x] 2.5 确认所有 push contribution 默认进入同一个合成池，不新增 action spec composition group。

## 3. Arbitration integration

- [x] 3.1 在 `ActionArbiter` 附近新增 push vector composition 入口。
- [x] 3.2 在同 tick ready push action 进入普通 move arbitration 前，按 subject key 聚合同 tick contribution。
- [x] 3.3 将零向量结果标记为 cancelled，不再产生移动或 deferred 输出。
- [x] 3.4 将单轴非零向量转换为一个单位方向 push action 或 move request。
- [x] 3.5 对双轴非零向量采用 X then Y 的确定性单格路径拆分。

## 4. Collision-safe movement

- [x] 4.1 确认合成结果不能直接跳到向量终点。
- [x] 4.2 单轴结果继续经过现有 body claim、target claim 和 commit validation。
- [x] 4.3 对双轴路径逐步验证每个单格步骤，任一步阻挡都不能穿越。
- [x] 4.4 记录被取消、被阻挡或等待路径策略的 reason，方便日志复现。
- [x] 4.5 预留未来 8 向输入的碰撞规则边界，不在第一版隐式允许 diagonal 穿角。

## 5. Automated tests

- [x] 5.1 Unity TestFramework EditMode：同 tick 同 subject 的 Up + Down 抵消，不产生下一步移动。
- [x] 5.2 Unity TestFramework EditMode：同 tick 同 subject 的两个 Up 合成为一个 Up，contribution count 为 2。
- [x] 5.3 Unity TestFramework EditMode：同 tick 不同 subject 的 push 不互相合成。
- [x] 5.4 Unity TestFramework EditMode：合成后的单轴移动仍被阻挡体阻挡，不能穿过碰撞。
- [x] 5.5 Unity TestFramework EditMode：metadata 保留 per-direction contribution 和 energy placeholder，但不改变移动距离。
- [x] 5.6 Unity TestFramework EditMode：双轴净向量按 X then Y 拆分，并逐步检查碰撞。
- [x] 5.7 Server verification：复现上下震荡结构，确认相反方向同 tick 同 subject 被抵消或按策略归一，服务器不持续生成相反方向闭环。

## 6. Validation

- [x] 6.1 运行 `openspec validate add-push-vector-composition --strict --no-interactive`。
- [x] 6.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 6.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 6.4 运行 Unity TestFramework EditMode 相关测试。
- [x] 6.5 用户手动 Play Mode 复现 push loop，确认相反方向震荡按新规则收敛，且碰撞不会被向量移动跳过。
