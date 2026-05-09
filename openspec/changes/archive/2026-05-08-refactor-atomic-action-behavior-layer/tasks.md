## 1. Proposal Gate
- [x] 1.1 审阅 `proposal.md`，确认本 change 只规划原子行为单元层。
- [x] 1.2 审阅 `design.md`，确认不做完整 GAS、不做客户端预测、不做同 tick 完整推链事务。
- [x] 1.3 审阅 `atomic-action-behavior-layer` spec delta，确认 action unit 生命周期清晰。
- [x] 1.4 审阅 `claim-driven-action-arbitration` spec delta，确认 pending push 语义改为 parent / derived / retry。
- [x] 1.5 审阅 `data-driven-runtime-actions` spec delta，确认 `ActionRequest` 与 action unit 生命周期边界清晰。
- [x] 1.6 审阅 `shared-gamecore-entity-rules` spec delta，确认旧 action / intent / pending 描述已收口。
- [x] 1.7 获得用户批准后再进入 apply 阶段。

## 2. Current Code Audit
- [x] 2.1 标记 `WorldAction.CostTicks / CreatedTick / ReadyTick` 的现有语义。
- [x] 2.2 标记 `ActionRequest.CreatedTick / ReadyTick` 的传递路径。
- [x] 2.3 标记 `PushPropagationState.CurrentFrontEntityId` 的旧 front 链语义。
- [x] 2.4 标记 `ActionRequestAdapter.FromPendingPush` 丢失 parent action unit 的位置。
- [x] 2.5 标记 `ResolvePushableBlock` 直接 `AddPush` 的旧 pending 入口。
- [x] 2.6 标记 `StateDrivenRules.ApplyProposalResultsToPendingStates` 直接 `MarkMoveAccepted` 的问题。
- [x] 2.7 标记 `RulePlanner.TryPlanMove(AcceptedAction)` 中 action unit claims 的接入点。
- [x] 2.8 标记 `ConflictResolver` 保持单 unit atomic commit 的边界。
- [x] 2.9 标记 `BehaviorIntent` 仍作为兼容 adapter 泄漏的位置。

## 3. Action Unit Model
- [x] 3.1 定义 action unit id 与 owner action id 的关系。
- [x] 3.2 定义 action unit lifecycle 状态。
- [x] 3.3 定义 created tick、ready tick、cost ticks 的统一字段来源。
- [x] 3.4 定义 parent action unit 等待 derived action unit 的状态。
- [x] 3.5 定义 derived action unit 成功后的 parent retry tick 规则，默认 `RetryCostTicks = 1`。
- [x] 3.6 定义 derived action unit 失败后的 parent fail 规则。
- [x] 3.7 定义 `OwnerActionId`、`ActionUnitId`、`ParentUnitId`、`DerivedFromUnitId`。
- [x] 3.8 定义 retry count、pending timeout、visited entity / action 防护。
- [x] 3.9 定义第二层 pushable blocker 会阻断并失败，当前业务不做传递推。
- [x] 3.10 定义 `ActionRequest` 只作为输入，action unit state 保存生命周期。

## 4. Arbitration Semantics
- [x] 4.1 让 ready action unit 进入 `ActionArbiter`。
- [x] 4.2 让 accepted action unit 输出 claims。
- [x] 4.3 让 rejected action unit 输出稳定 reason。
- [x] 4.4 让 blocked-by-pushable action unit 输出 derived action unit proposal。
- [x] 4.5 让 parent action unit 进入 waiting/retry，而不是被 front 链替代。
- [x] 4.6 保持 bounce / reject policy 不进入 push-specific pending。
- [x] 4.7 保持 final Component / tag / world state 作为仲裁输入，不查询 Ability / RuntimeEffect。
- [x] 4.8 明确 action result 只有 parent unit 自己完成后才对 owner action 写 success。

## 5. Pending State Refactor
- [x] 5.1 新增 generic pending action unit state 或替换 `PushPropagationState`。
- [x] 5.2 保存 parent action unit 的 spec、source、target、priority、cost tick 信息。
- [x] 5.3 保存 derived action unit id 和 blocker entity id。
- [x] 5.4 支持 derived success 后 parent ready-to-retry。
- [x] 5.5 支持 derived fail 后 parent fail。
- [x] 5.6 移除 `CurrentFrontEntityId` 作为核心推进语义。
- [x] 5.7 保留或迁移 visited chain 作为安全防护，而不是行为结果来源。
- [x] 5.8 不支持 push 多层等待链，每个 action unit 同时最多等待一个 active derived unit。
- [x] 5.9 支持 pending timeout 和 retry limit 失败结果。

## 6. Execution Orchestration
- [x] 6.1 `StateDrivenRules` 收集 ready world actions。
- [x] 6.2 `StateDrivenRules` 收集 ready pending parent retries。
- [x] 6.3 `StateDrivenRules` 收集 ready derived action units。
- [x] 6.4 arbitration 后将 accepted units 交给 planner。
- [x] 6.5 commit 后根据 derived result 更新 parent pending 状态。
- [x] 6.6 commit 后只在 parent unit 自己 accepted 时写入 parent action success result。
- [x] 6.7 移除 push/front 专用完成分支。
- [x] 6.8 保持 `ConflictResolver` 只负责单 tick accepted plans 原子 commit。

## 7. Tests
- [x] 7.1 新增普通 pushable：root action 被 blocker 阻挡后进入 waiting。
- [x] 7.2 新增普通 pushable：derived push 成功后 parent 下一 tick retry。
- [x] 7.3 新增普通 pushable：parent retry 后原始移动成功。
- [x] 7.4 新增 port-connected body：linked member 撞 pushable 后 parent waiting。
- [x] 7.5 新增 port-connected body：pushable derived action 成功后 parent retry。
- [x] 7.6 新增 port-connected body：parent retry 后 body members 原子移动成功。
- [x] 7.7 新增 derived action 失败后 parent fail。
- [x] 7.8 新增 retry limit / visited entity 防护测试。
- [x] 7.9 新增第二层 pushable blocker 不移动并失败的测试。
- [x] 7.10 新增 action unit id / owner action id / parent id / derived id 关系测试。
- [x] 7.11 更新 `AuthoritativeMoveVerification` 覆盖 split-tick action unit 语义。
- [x] 7.12 更新或替换旧 `PendingPushContinuation_UsesActionArbiter` 断言。

## 8. Validation
- [x] 8.1 运行 `openspec validate refactor-atomic-action-behavior-layer --strict --no-interactive`。
- [x] 8.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 8.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 8.4 运行 Unity TestFramework EditMode 相关测试。
- [x] 8.5 不执行 Unity Player build。
- [x] 8.6 给出 Play Mode 手动端到端验证步骤。

## 9. Completion Audit
- [x] 9.1 确认普通 pushable 和 port-connected body push 使用同一 pending/retry 机制。
- [x] 9.2 确认每个 action unit 内部 claims 原子提交。
- [x] 9.3 确认 action unit 之间按 cost tick 分阶段推进。
- [x] 9.4 确认 `StateDrivenRules` 不保留 front/push/port 特例分支。
- [x] 9.5 确认 System / Rules 不查询 Ability / RuntimeEffect。
- [x] 9.6 确认相关 specs 的旧 action / intent / pending 语义已同步收口。
- [x] 9.7 确认所有任务完成后再更新 checklist。

