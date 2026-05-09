## 1. Scope And Baseline
- [x] 1.1 阅读 `docs/Goal/atomic-transaction-and-emergent-motion.md` 并确认实现只覆盖原子事务边界。
- [x] 1.2 对齐 active change `fix-multi-contact-push-propagation`，确认没有重复修改同一测试语义。
- [x] 1.3 记录当前 `push chain cycle` 复现日志中的 candidate / chain / snapshot。
- [x] 1.4 将“涌动增幅 push 机器”写入目标文档和本 change 约束。
- [x] 1.5 明确第一阶段不实现 strength 增幅，只实现周期 deferred output。

## 2. Atomic Consumption Boundary
- [x] 2.1 定义 subject stable key 的运行时比较方式。
- [x] 2.2 增加同一 transaction / tick 内 seen subject 记录。
- [x] 2.3 将完全相同 subject 的重复命中识别为 convergence。
- [x] 2.4 保持不同 subject 部分重叠为 unsafe overlap 或 `push chain cycle`。
- [x] 2.5 确认同一 subject 同 tick 不会生成第二个 action unit。
- [x] 2.6 确认 subject 去重不依赖 contact count。

## 3. Push Output Deferral
- [x] 3.1 定义 deferred output 的最小运行时字段：source subject、target subject、intent、direction/vector、ready tick、cost、dedupe key、causality id。
- [x] 3.2 将 push propagation 从 pending child unit 迁移到 deferred output。
- [x] 3.3 确认同 tick feedback 不会立即重新进入 arbitration。
- [x] 3.4 确认没有外部输出的闭环不会让 owner action 永久 pending。
- [x] 3.5 删除 push 对 `AddHandoffActionState()` / `TryAddChildUnits()` 的依赖。
- [x] 3.6 增加 `DeferredAction` 或 `ExplicitOutputEvent` 运行时结构。
- [x] 3.7 `ActionSpecs` 在 blocked push 时生成 deferred output，而不是 pending child。
- [x] 3.8 `StateDrivenRules` 汇总 deferred outputs。
- [x] 3.9 `AuthoritativeWorldTickRunner` 将 ready deferred outputs 放回 `WorldActionQueue`。
- [x] 3.10 区分 `bounded/no-output` 与 `bounded/deferred-output` 诊断。
- [x] 3.11 确认 deferred output 行为来自 policy 字段，不依赖 action 名字、entity 名字、tag 组合或硬编码 mechanism 名。
- [x] 3.12 确认 push source action 不等待 downstream deferred action 的结果。
- [x] 3.13 搜索 `PendingRuleStates` 的非 push 调用方；如果没有真实调用方，删除或隔离 pending push 链条。
- [x] 3.14 若未来等待型 action 仍有需求，记录为单独 proposal，不在本 change 保留备用链条。

## 4. Tests
- [x] 4.1 增加 EditMode 测试：同一 tick 多 push 命中同一 subject，只消费一次。
- [x] 4.2 增加 EditMode 测试：多分支收敛到同一 subject，不报 cycle，且不创建重复 deferred output。
- [x] 4.3 增加 EditMode 测试：不同 subject 部分重叠仍失败。
- [x] 4.4 增加 EditMode 测试：闭环 feedback 不创建 pending child。
- [x] 4.5 增加 EditMode 测试：闭环有外部输出时生成 deferred output，并按 cost 在后续 tick 继续触发。
- [x] 4.6 增加 EditMode 测试：闭环无外部输出时本体稳定不动且不生成 deferred output。
- [x] 4.7 增加 EditMode 测试：push action 不等待 downstream deferred action 的结果。
- [x] 4.8 增加 EditMode 测试：`bounded/no-output` 日志中的复现例应变为 deferred output 或明确无输出。
- [x] 4.9 增加 EditMode 测试：涌动机器在无 strength policy 时按 `period = cost` 输出且 strength 不增长。
- [x] 4.10 增加 EditMode 测试：两个不同 `ActionSpecId` 使用相同 deferred output policy 时行为一致。
- [x] 4.11 增加 EditMode 测试：push blocked path 不调用 pending child handoff。

## 5. Validation
- [x] 5.1 运行 `openspec validate define-atomic-transaction-boundary --strict --no-interactive`。
- [x] 5.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 5.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 5.4 运行相关 Unity TestFramework EditMode 测试。
- [x] 5.5 检查 Unity console 无编译错误。
- [ ] 5.6 手动 Play Mode 验证：闭环装置不会单次 action 挂死。
- [ ] 5.7 手动双客户端验证：闭环输出和 WorldDelta 在两个客户端一致。
