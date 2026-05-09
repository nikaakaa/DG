## 1. Proposal Gate
- [x] 1.1 审阅 `proposal.md`，确认本提案只处理 claim-driven arbitration。
- [x] 1.2 审阅 `design.md`，确认不扩展 Ability / GAS。
- [x] 1.3 审阅 spec delta，确认执行层职责边界清晰。
- [x] 1.4 获得用户批准后再进入 apply 阶段。

## 2. Current Code Audit
- [x] 2.1 标记 `StateDrivenRules.ProcessMove` 中 target 解析职责。
- [x] 2.2 标记 `StateDrivenRules.ProcessMove` 中 blocked policy 职责。
- [x] 2.3 标记 `StateDrivenRules.FindBlockingForBodyMove` 的迁移目标。
- [x] 2.4 标记 `StateDrivenRules.AdvancePushStates` 的迁移目标。
- [x] 2.5 标记 `IntentArbiter` 中 tag / movement permission / conflict 职责。
- [x] 2.6 标记 `RulePlanner` 中 body projection / occupancy 重复职责。

## 3. Arbitration Result Model
- [x] 3.1 定义 `AcceptedAction`。
- [x] 3.2 定义 `RejectedAction`。
- [x] 3.3 定义 `DerivedAction` 或 pending continuation 结果。
- [x] 3.4 定义 `ActionArbitrationResult`。
- [x] 3.5 定义 action result 写回规则。
- [x] 3.6 定义 commit proposal 输出规则。

## 4. Claim Generation
- [x] 4.1 将 move target 解析迁入 `ActionArbiter`。
- [x] 4.2 将 port-connected body 解析迁入 `ActionArbiter`。
- [x] 4.3 为每个 body member 生成 body move claim。
- [x] 4.4 生成或推导目标格独占 claim。
- [x] 4.5 忽略同 body 内部旧格占用。
- [x] 4.6 检测 body 外部 blocker。
- [x] 4.7 覆盖 linked port member 撞 pushable 的 claim。

## 5. Policy Arbitration
- [x] 5.1 将 movement permission 检查迁入 `ActionArbiter`。
- [x] 5.2 将 required tags 检查迁入 `ActionArbiter`。
- [x] 5.3 将 blocked tags 检查迁入 `ActionArbiter`。
- [x] 5.4 将 `StartPushIfPushable` 迁入 `ActionArbiter`。
- [x] 5.5 将 `BounceIfBouncable` 迁入 `ActionArbiter`。
- [x] 5.6 将 `Reject` blocked policy 迁入 `ActionArbiter`。
- [x] 5.7 将 same-claim merge 迁入 `ActionArbiter`。
- [x] 5.8 将 conflicting body claim 判断迁入 `ActionArbiter`。
- [x] 5.9 将 priority interrupt 迁入 `ActionArbiter`。

## 6. Pending Push
- [x] 6.1 让 pending push state 生成统一 `ActionRequest`。
- [x] 6.2 让 pending push continuation 走 `ActionArbiter`。
- [x] 6.3 移除 system 层对 port front 的特殊 AddPushIntent 分支。
- [x] 6.4 保持 push chain、loop、already pending 的结果兼容。
- [x] 6.5 覆盖 port-connected body 推动链。

## 7. Planner And Execution
- [x] 7.1 让 `RulePlanner` 支持从 `AcceptedAction` 生成 `MovePlan`。
- [x] 7.2 让 planner 不再重复决定 pushable / blocker policy。
- [x] 7.3 简化 `StateDrivenRules.Tick` 为 orchestration。
- [x] 7.4 移除 `StateDrivenRules.ProcessPushableBlock`。
- [x] 7.5 移除 `StateDrivenRules.ProcessBounceBlock`。
- [x] 7.6 移除 `StateDrivenRules.FindBlockingForBodyMove`。
- [x] 7.7 降级或移除主流程中的 `IntentArbiter`。

## 8. Tests
- [x] 8.1 新增 claim-driven registry / adapter EditMode 测试。
- [x] 8.2 新增 root player 撞 pushable 测试。
- [x] 8.3 新增 linked port member 撞 pushable 测试。
- [x] 8.4 新增 port-connected body 外部 blocker 拒绝测试。
- [x] 8.5 新增 same-claim merge 测试。
- [x] 8.6 新增 conflicting body claim 测试。
- [x] 8.7 新增 priority interrupt 测试。
- [x] 8.8 新增 auto bounce proposal 测试。
- [x] 8.9 新增 pending push continuation 走 ActionArbiter 测试。
- [x] 8.10 更新 `AuthoritativeMoveVerification` 覆盖迁移后等价行为。

## 9. Validation
- [x] 9.1 运行 `openspec validate refactor-action-claim-arbitration --strict --no-interactive`。
- [x] 9.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 9.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 9.4 运行 Unity TestFramework EditMode 相关测试。（已完成：Unity MCP EditMode 运行 `DG.EditorTests.DataDrivenRuntimeActionTests`，12/12 通过）
- [x] 9.5 不执行 Unity Player build。
- [x] 9.6 给出 Play Mode 手动端到端验证步骤。

## 10. Completion Audit
- [x] 10.1 确认 `StateDrivenRules` 不再处理 move target / blocker / pushable / bounce 仲裁。
- [x] 10.2 确认主流程使用 `ActionClaim` 做冲突、合并、打断。
- [x] 10.3 确认 planner 消费 accepted action / claims。
- [x] 10.4 确认新增普通 move 行为不需要修改核心 execution / arbitration orchestration。
- [x] 10.5 确认所有任务完成后再更新 checklist。
