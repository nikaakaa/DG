## 1. Proposal Gate
- [x] 1.1 审阅 `proposal.md`，确认目标是整个行为层统一入口，不是 move 管线补丁。
- [x] 1.2 审阅 `design.md`，确认 action unit 原子边界、handoff、no parent retry 和 port-connected 非目标。
- [x] 1.3 审阅 `shared-gamecore-entity-rules` spec delta，确认 state-driven 入口语义已改为 behavior action unit。
- [x] 1.4 审阅 `claim-driven-action-arbitration` spec delta，确认 body member claim 和 pending retry 不再作为普通行为真相。
- [x] 1.5 审阅 `data-driven-runtime-actions` spec delta，确认运行时行为请求与 action unit 结果分支边界清晰。
- [x] 1.6 获得用户批准后再进入 apply 阶段。

## 2. Current Code Audit
- [x] 2.1 标记 `ActionSpecs.cs` 中 `ActionRequest`、`ActionClaim`、`AcceptedAction`、`DerivedAction` 的当前职责。
- [x] 2.2 标记 `ActionArbiter.TryBuildMoveClaims` 生成 body member claims 的位置。
- [x] 2.3 标记 `BodyResolver` / `PortConnectionSystem` 把多个 entity 聚合为 body 的位置。
- [x] 2.4 标记 `RulePlanner.TryPlanMove(AcceptedAction)` 从 claims 生成 `MovePlan.Members` 的位置。
- [x] 2.5 标记 `ConflictResolver.Resolve` 循环提交多个 members 的位置。
- [x] 2.6 标记 `PendingActionState.MarkUnitAccepted` 让 parent retry 的位置。
- [x] 2.7 标记 `StateDrivenRules.ApplyProposalResultsToPendingStates` 等 parent completed 才写 owner result 的位置。
- [x] 2.8 标记当前测试中表达 parent retry、body atomic commit、nested blocked 的断言。

## 3. Behavior Action Unit Intake
- [x] 3.1 定义 behavior action unit 输入类型或重命名当前 `ActionRequest` 边界。
- [x] 3.2 定义所有来源进入同一 intake：player input、auto tick、mechanism trigger、runtime result、debug command、push handoff。
- [x] 3.3 定义 action unit id、owner action id、source entity id、target entity id、source context、tick、priority。
- [x] 3.4 定义 action unit result branch：success、failed、handoff、noop。
- [x] 3.5 定义 handoff result 必须包含目标 entity、方向、来源 action id 和 handoff reason。
- [x] 3.6 定义 source action 在 handoff 后结束，不进入 waiting/retry。

## 4. Push Handoff Semantics
- [x] 4.1 将 `StartPushIfPushable` 从 parent pending/retry 改为 handoff result。
- [x] 4.2 A 命中可推 B 时，A 不移动，A 的 action 写 handoff 结果。
- [x] 4.3 B 作为独立 action unit 进入后续 intake。
- [x] 4.4 B 若目标为空，B 自己移动。
- [x] 4.5 B 若目标仍有可推 C，B 再产生 handoff，B 自己结束。
- [x] 4.6 B 若目标不可推或被 player 占用，B 失败，A 不 retroactively retry。
- [x] 4.7 移除或隔离 `PendingActionState` 中普通 push parent retry 成功路径。
- [x] 4.8 保留循环、重复 active action、timeout 等安全保护，但保护对象改为 action handoff 链。

## 5. Single Unit Claim / Plan / Commit
- [x] 5.1 将默认 movement claim 改为当前 action unit 的源对象或 entity abstraction claim。
- [x] 5.2 planner 从单 action unit claim 生成单 unit plan。
- [x] 5.3 commit 一次只提交当前 action unit 的结果。
- [x] 5.4 同 tick 多 action unit 的冲突仍由 priority / conflict policy 裁决。
- [x] 5.5 移除普通路径对 `MovePlan.Members` 多实体提交的依赖。
- [x] 5.6 保留 `BehaviorBody` / port-connected 兼容层，但不让其成为普通 action unit 原子边界。
- [x] 5.7 明确 port-connected body 的最终模型不在本 change 内完成。

## 6. Execution Orchestration
- [x] 6.1 `StateDrivenRules` 收集 ready behavior action units。
- [x] 6.2 `StateDrivenRules` 处理 handoff 结果并把目标 action unit 排入后续 intake。
- [x] 6.3 `StateDrivenRules` 在 source action handoff 后立即写 action result。
- [x] 6.4 `StateDrivenRules` 不等待 derived action 成功后再写 source action success。
- [x] 6.5 `StateDrivenRules` 不包含 player move、auto move、mechanism push、debug move 的业务分支。
- [x] 6.6 dirty/delta 只来自实际提交的 action unit 状态变化。

## 7. Tests
- [x] 7.1 新增 A 推 B：A handoff 成功但 A 坐标保持原位。
- [x] 7.2 新增 A 推 B 后 B 独立 action unit 移动到空格。
- [x] 7.3 新增 A 推 B、B 推 C：A 和 B 都以 handoff 分支结束，移动由后续 action unit 独立决定。
- [x] 7.4 新增 B 目标不可推：B 失败，A 不 retry。
- [x] 7.5 新增 source action handoff 后不会生成 parent retry。
- [x] 7.6 新增单 action unit commit 不移动多个普通 entity。
- [x] 7.7 新增同 tick 多 action unit 冲突按 priority / conflict policy 处理。
- [x] 7.8 更新或删除 `RetriesParentAfterDerivedSuccess` 类旧断言。
- [x] 7.9 更新或隔离 port-connected body 测试，避免它定义普通 action unit 原子边界。
- [x] 7.10 更新 `AuthoritativeMoveVerification` 覆盖 handoff 和 no parent retry。

## 8. Validation
- [x] 8.1 运行 `openspec validate refactor-unified-behavior-action-entry --strict --no-interactive`。
- [x] 8.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 8.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 8.4 运行 Unity TestFramework EditMode 相关测试。
- [x] 8.5 不执行 Unity Player build。
- [x] 8.6 给出 Play Mode 双客户端手动验证步骤。

## 9. Completion Audit
- [x] 9.1 确认所有运行时行为来源都通过 behavior action unit intake。
- [x] 9.2 确认普通 action unit 不再提交多个普通 entity。
- [x] 9.3 确认 push handoff 后 source action 不 retry。
- [x] 9.4 确认 handoff 目标 action unit 独立裁决自己的移动/失败/继续 handoff。
- [x] 9.5 确认 port-connected body 没有被误报为本 change 的最终完成项。
- [x] 9.6 确认 specs、tests、design 对“统一行为层入口”的表述一致。
- [x] 9.7 确认所有任务完成后再更新 checklist。
