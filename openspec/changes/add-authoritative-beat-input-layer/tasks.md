## 1. 输入模型与服务端缓冲
- [ ] 1.1 定义普通玩家输入状态：Buffered、Replaced、Consumed、Expired、Rejected、Resolved。
- [ ] 1.2 定义服务端拍点输入数据，包含 entityId、beatTick、direction、clientInputId、clientTick、submitSequence。
- [ ] 1.3 新增服务端拍点输入缓冲，使用 `beatTick + entityId` 作为唯一 key。
- [ ] 1.4 实现同一实体同一拍后输入覆盖前输入，并立即完成被覆盖输入为 Replaced。
- [ ] 1.5 实现过期输入拒绝，不进入后续 action queue。
- [ ] 1.6 实现 `Drain(beatTick)` 只返回当前拍最终输入，且 drain 后不可重复消费。
- [ ] 1.7 保持 debug spawn、debug move、debug remove、runtime effect debug 不走普通玩家输入缓冲。

## 2. 服务端 tick 接入
- [ ] 2.1 调整 `AuthoritativeWorldTickRunner`，每个 serverTick 只消费当前拍输入。
- [ ] 2.2 用服务端权威当前位置和 direction 计算玩家移动目标格。
- [ ] 2.3 将最终输入转换为现有 player movement action，继续进入 action pipeline。
- [ ] 2.4 保持 auto move、push on enter、deferred action、debug action 的既有顺序和语义。
- [ ] 2.5 确保输入替换、过期、runner stop 时不会留下未完成等待。
- [ ] 2.6 更新服务端日志，能看到 beatTick、entityId、direction、输入状态和最终 action 结果。

## 3. 协议与客户端输入提交
- [ ] 3.1 在 Outer 协议中增加普通玩家输入意图请求/响应，字段包含 entityId、clientInputId、beatTick、direction、clientTick。
- [ ] 3.2 导出协议，确认服务端和 Unity 客户端生成代码更新。
- [ ] 3.3 新增或调整服务端 Handler，将输入协议转换为拍点输入缓冲提交。
- [ ] 3.4 保留旧移动请求兼容路径时，必须把目标格适配为方向意图，并进入同一拍点输入缓冲。
- [ ] 3.5 调整客户端普通输入提交边界，普通移动提交 direction/beat intention，不直接把客户端目标格当作权威裁决输入。
- [ ] 3.6 客户端普通输入失败时只记录失败或清理表现反馈，不在服务端权威模式下 fallback 到本地规则。

## 4. 客户端表现边界
- [ ] 4.1 增加客户端待确认输入记录，按 clientInputId 追踪输入状态。
- [ ] 4.2 增加非权威方向反馈入口，不修改 `ClientMapWorld.CoreWorld` 权威坐标。
- [ ] 4.3 收到 WorldDelta 后以服务端 snapshot/delta 更新镜像，并清理已解决输入。
- [ ] 4.4 输入被 Replaced、Expired 或 Rejected 时清理对应非权威反馈。

## 5. 自动验证
- [ ] 5.1 增加 Unity TestFramework EditMode 测试：同实体同拍多输入只保留最后一个。
- [ ] 5.2 增加 Unity TestFramework EditMode 测试：不同实体同一拍互不覆盖。
- [ ] 5.3 增加 Unity TestFramework EditMode 测试：被替换输入完成为 Replaced。
- [ ] 5.4 增加 Unity TestFramework EditMode 测试：过期输入不会进入 action queue。
- [ ] 5.5 增加 Unity TestFramework EditMode 测试：未来拍输入不会提前消费。
- [ ] 5.6 增加 Unity TestFramework EditMode 测试：当前拍 drain 后不会重复消费。
- [ ] 5.7 增加服务端 authoritative verification：一拍快速提交多个方向只产生一个 player movement action。
- [ ] 5.8 增加服务端 authoritative verification：普通输入仍通过 action pipeline 触发推动、阻挡和 WorldDelta。
- [ ] 5.9 运行 `openspec validate add-authoritative-beat-input-layer --strict --no-interactive`。
- [ ] 5.10 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [ ] 5.11 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [ ] 5.12 运行 Unity TestFramework EditMode 覆盖本变更新增输入层测试。

## 6. 手动端到端验证
- [ ] 6.1 用户启动 Fantasy 服务端。
- [ ] 6.2 用户启动两个 Unity Play Mode 客户端并 JoinWorld。
- [ ] 6.3 用户在一个 beat 窗口内快速按多个方向，确认只结算最后方向。
- [ ] 6.4 用户确认两个客户端最终通过 WorldDelta 收敛到同一权威坐标。
- [ ] 6.5 用户验证阻挡、推动、机关触发仍由服务端裁决，客户端不本地决定最终坐标。
- [ ] 6.6 用户验证 debug spawn/move/remove/runtime effect 不受普通玩家输入缓冲破坏。
