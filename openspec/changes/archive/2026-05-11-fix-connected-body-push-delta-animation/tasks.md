## 1. 服务端规则链路排查
- [x] 1.1 在 `VerifyStateDrivenPush` 中补充玩家推动连接体的失败复现场景。
- [x] 1.2 断言 handoff 生成的 deferred action 保留连接体全员 subject ids。
- [x] 1.3 断言 pending action unit 中的 subject ids 未退化为单个 entry entity。
- [x] 1.4 断言 accepted action claims 覆盖连接体每个成员。
- [x] 1.5 保留单实体 push 的既有成功路径验证。

## 2. 服务端修复
- [x] 2.1 修复 blocked policy / handoff subject 解析中丢失连接体 subject 的位置。
- [x] 2.2 修复 deferred action 入队或合并时丢失 subject ids 的位置。
- [x] 2.3 修复 pending child / ready unit 生成时 subject ids 退化的问题。
- [x] 2.4 修复 accepted action 到 `MovePlan` 的 claims 映射，确保 connected body 成员全员进入 plan。
- [x] 2.5 确保 `ConflictResolver` 提交全员后每个成员都被 `GameWorld.MoveEntity` 标记 dirty。
- [x] 2.6 确保 `AuthoritativeWorldTickRunner` 为每个已移动成员记录 animation metadata。
- [x] 2.7 确保 source handoff 不把 downstream body 合并为 source 的提交成员。
- [x] 2.8 确保普通 push 链不生成 parent retry，中间 subject 只传递 push。
- [x] 2.9 确保 `"bounded/deferred-output"` 为 subject members 生成 push feedback metadata。
- [x] 2.10 确保 metadata-only `WorldDelta` 可以广播给客户端。

## 3. 客户端表现边界
- [x] 3.1 撤掉或改写“客户端不补齐缺失连接体成员”的反向测试。
- [x] 3.2 增加客户端测试：服务端 delta 包含连接体全员时，`ClientAnimationLayer` 为每个成员生成动画事件。
- [x] 3.3 增加客户端测试：`ClientWorldVisuals` 能同时持有多个连接体成员的 active animation。
- [x] 3.4 确认客户端不根据本地 port graph 猜测缺失成员。
- [x] 3.5 确认连续 delta 下每个连接体成员最终落到最新服务端坐标。

## 4. 自动化验证
- [x] 4.1 运行 `openspec validate fix-connected-body-push-delta-animation --strict --no-interactive`。
- [x] 4.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 4.3 使用 Unity TestFramework EditMode 运行连接体推动相关客户端测试。
- [x] 4.4 检查 Unity Console 没有新增编译错误。

## 5. 手动验证
- [x] 5.1 用户手动 Play Mode 验证：直接推动 downstream connected body 时，所有实际移动成员整体移动。
- [x] 5.2 用户手动 Play Mode 验证：所有实际移动成员都有移动动画，不只受力成员或第一个受力位置。
- [x] 5.3 用户手动双客户端验证：观察端不移动也能看到相同 push feedback、尾部移动动画和最终坐标。
- [x] 5.4 用户手动 Play Mode 验证：普通连续 push 链中间连接体只播放 push feedback，不产生位移；尾部实际可移动 subject 才移动。
- [x] 5.5 用户确认手动结果后再考虑归档。
