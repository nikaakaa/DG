## 1. Proposal Gate
- [x] 1.1 审阅 `proposal.md`，确认目标是配置层和运行时层分离。
- [x] 1.2 审阅 `design.md`，确认不在本阶段引入 Ability。
- [x] 1.3 审阅 spec delta，确认新增普通行为不改核心 system。
- [x] 1.4 获得用户批准后再进入 apply 阶段。

## 2. Current Behavior Audit
- [x] 2.1 列出现有 `WorldActionKind` 到新 `ActionSpec` 的映射。
- [x] 2.2 列出现有 `BehaviorIntentKind` 到新 source / policy 的映射。
- [x] 2.3 标记 `StateDrivenRuleExecutionSystem` 中每个业务分支的职责归属。
- [x] 2.4 标记哪些逻辑属于 adapter。
- [x] 2.5 标记哪些逻辑属于 arbitration。
- [x] 2.6 标记哪些逻辑属于 planner。
- [x] 2.7 标记哪些逻辑属于 commit。

## 3. Schema Model
- [x] 3.1 定义 `ActionPrimitive`。
- [x] 3.2 定义 `ActionSpecId`。
- [x] 3.3 定义 `ActionSpec`。
- [x] 3.4 定义 `ActionRequest`。
- [x] 3.5 定义 `ActionSourceContext`。
- [x] 3.6 定义 `ActionTarget`。
- [x] 3.7 定义 `ActionRuntimeParams`。
- [x] 3.8 定义 `ActionClaim`。
- [x] 3.9 定义 `ConflictPolicy`。
- [x] 3.10 定义 `InterruptPolicy`。
- [x] 3.11 定义 `MergePolicy`。
- [x] 3.12 定义 `PlanRule`。
- [x] 3.13 定义 `CommitRule`。

## 4. Registry And Adapter
- [x] 4.1 建立最小 `ActionSpecRegistry`。
- [x] 4.2 加入玩家移动 spec。
- [x] 4.3 加入自动移动 spec。
- [x] 4.4 加入机关推动 spec。
- [x] 4.5 加入调试移动 spec。
- [x] 4.6 加入调试生成 spec。
- [x] 4.7 加入调试删除 spec。
- [x] 4.8 建立旧 `WorldAction` 到 `ActionRequest` 的 adapter。
- [x] 4.9 保留旧调用点的兼容入口。

## 5. Arbitration Refactor
- [ ] 5.1 让仲裁层从 `ActionRequest + ActionSpec` 生成 claim 并用于主仲裁流程。
- [x] 5.2 让仲裁层根据 final Component result 判断 required / blocked component。
- [x] 5.3 让仲裁层根据 tag 判断 required / blocked tag。
- [ ] 5.4 让仲裁层根据 conflict group 判断同 tick 冲突。
- [ ] 5.5 让仲裁层根据 merge policy 合并同 claim。
- [x] 5.6 让仲裁层根据 interrupt policy 打断低优先级请求。
- [x] 5.7 让 pending push 状态通过统一 request / claim 继续推进。
- [x] 5.8 移除核心仲裁里的新增业务 kind 需求。

## 6. Planning And Commit
- [ ] 6.1 让 planner 直接读取统一 accepted action。
- [x] 6.2 保持移动 body 解析和占格规划。
- [x] 6.3 保持 port-connected body 的整体移动。
- [x] 6.4 将自动移动反弹方向变化表达为 commit rule / proposal。
- [x] 6.5 将 auto move tick 更新表达为 commit rule / proposal。
- [x] 6.6 将 spawn 表达为统一 create proposal。
- [x] 6.7 将 remove 表达为统一 delete proposal。
- [x] 6.8 确认执行层不再按普通业务 action kind 分类。

## 7. Runtime Integration
- [ ] 7.1 调整 server tick runner 的玩家输入 action 创建。
- [ ] 7.2 调整 server tick runner 的 auto action 创建。
- [ ] 7.3 调整 server tick runner 的 mechanism trigger action 创建。
- [ ] 7.4 调整 debug input queue 的 action 创建。
- [ ] 7.5 调整 sandbox runner 的 action 创建。
- [x] 7.6 确认 `ClientMapWorld` 仍只镜像服务端 final state。

## 8. Tests
- [x] 8.1 新增 ActionSpec registry EditMode 测试。
- [x] 8.2 新增旧行为到新 request 映射 EditMode 测试。
- [x] 8.3 新增玩家移动等价 EditMode 测试。
- [x] 8.4 新增自动移动等价 EditMode 测试。
- [x] 8.5 新增机关推动等价 EditMode 测试。
- [x] 8.6 新增调试移动 / 生成 / 删除等价 EditMode 测试。
- [x] 8.7 新增同 body 同 claim 合并测试。
- [x] 8.8 新增同 body 不同 claim 冲突测试。
- [x] 8.9 新增高优先级打断低优先级测试。
- [x] 8.10 新增 blocked tag / final component 阻挡测试。
- [x] 8.11 新增配置化普通行为不改核心分支的测试或结构检查。
- [x] 8.12 更新 `Server/Tests/AuthoritativeMoveVerification` 覆盖迁移后等价行为。

## 9. Validation
- [x] 9.1 运行 `openspec validate refactor-data-driven-runtime-actions --strict --no-interactive`。
- [ ] 9.2 运行 Unity TestFramework EditMode 相关测试。
- [x] 9.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 9.4 不执行 Unity Player build。
- [x] 9.5 给用户手动 Play Mode 验证步骤。
- [ ] 9.6 用户手动验证服务端权威路径。
- [ ] 9.7 用户手动验证双客户端 WorldDelta 同步。

## 10. Completion Audit
- [x] 10.1 确认新增普通行为无需修改核心 execution 分支。
- [x] 10.2 确认新增普通行为无需修改核心 arbitration 分支。
- [x] 10.3 确认运行时 request 不复制配置层静态策略。
- [x] 10.4 确认 system 不查询 runtime effect / ability 状态。
- [ ] 10.5 确认所有任务完成后再更新 checklist。
