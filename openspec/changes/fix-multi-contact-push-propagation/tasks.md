## 1. 当前行为确认
- [x] 1.1 阅读 `ActionSpecs.cs` 中 `ProcessMoveRequest()` 的 move claim 生成流程。
- [x] 1.2 阅读 `BodyCapabilityResolver.FindExternalBlocking()` 的单 blocker 查询逻辑。
- [x] 1.3 阅读 `ActionSpecs.cs` 中 `ResolveBlocked()` 的 blocked policy 分支。
- [x] 1.4 阅读 `ActionSpecs.cs` 中 `ResolvePushableBlock()` 的单 blocker handoff 流程。
- [x] 1.5 阅读 `PendingRuleStates.cs` 中 `TryAddChildUnit()` 的单 child 限制。
- [x] 1.6 阅读 `StateDrivenRules.cs` 中 ready unit 收集和 pending 更新流程。
- [x] 1.7 增加当前行为保护测试：单 blocker push 仍保持现有结果。

## 2. Contact Set
- [x] 2.1 增加 external push contact 数据结构。
- [x] 2.2 将 `FindExternalBlocking()` 替换或扩展为 contact set 查询。
- [x] 2.3 contact set 查询只遍历 `BodyMove` claims。
- [x] 2.4 contact set 查询遍历所有 `BodyMove` claims 的 `ToCoord`。
- [x] 2.5 contact set 查询排除当前 moving body 的成员。
- [x] 2.6 contact set 查询只收集 blocking entities。
- [x] 2.7 contact set 查询保留命中 contact 事实，不决定 handoff subject。
- [x] 2.8 contact set 查询保持 deterministic ordering。
- [x] 2.9 增加测试：connected body 两个目标格收集两个 contacts。
- [x] 2.10 增加测试：body 内部成员互换格子不产生 external contact。
- [x] 2.11 增加测试：重复命中同一下游 subject 的 contacts 在 child 创建前仍可被观察。

## 3. 多 contact blocked 处理
- [x] 3.1 将 `ProcessMoveRequest()` 从单 blocker 分支改为 contact set 分支。
- [x] 3.2 无 contact 时保持现有普通 arbitration 路径。
- [x] 3.3 单 contact 时保持现有 push 行为兼容。
- [x] 3.4 多 contact 且 `BlockedPolicy = Reject` 时稳定失败。
- [x] 3.5 多 contact 且 `BlockedPolicy = BounceIfBouncable` 时保持当前单 blocker 可测边界，不在本 change 扩展 bounce 传播。
- [x] 3.6 多 contact 且 `BlockedPolicy = StartPushIfPushable` 时进入 push contact batch 创建流程。
- [x] 3.7 任一 contact 不可 push 或 handoff disabled 时 batch 不创建并稳定失败。
- [x] 3.8 增加测试：多 contact 中任一不可 push 导致当前 blocked step 失败。

## 4. Push Contact Batch
- [x] 4.1 在 pending 层增加最小 push contact batch 状态。
- [x] 4.2 batch 记录 parent unit id。
- [x] 4.3 batch 记录 child unit ids。
- [x] 4.4 batch 记录 created tick。
- [x] 4.5 batch 记录 lifecycle status。
- [x] 4.6 `PendingActionUnit` 记录 batch id 或可定位 batch 的等价字段。
- [x] 4.7 `ActionRequest` 不复制 batch lifecycle state。
- [x] 4.8 增加测试：child unit request 仍只携带 arbitration 所需 runtime 输入。

## 5. 多 child unit 创建
- [x] 5.1 将单 blocker handoff target 解析扩展为 contact 到 handoff target 的转换。
- [x] 5.2 每个 handoff target 保留 target entity id。
- [x] 5.3 每个 handoff target 保留 subject entity ids。
- [x] 5.4 每个 handoff target 的 spec id 来自 parent `ActionSpec.Handoff`。
- [x] 5.5 同一个 blocked step 为多个 contacts 创建同一个 push contact batch。
- [x] 5.6 同一个 batch 为每个 distinct downstream subject 创建一个 child unit。
- [x] 5.7 child units 共享 owner action id。
- [x] 5.8 child units 共享 parent unit id。
- [x] 5.9 child units 共享 direction。
- [x] 5.10 child units 共享 created tick。
- [x] 5.11 child units 共享 batch id。
- [x] 5.12 chain safety 禁止同一 batch 内重复 subject。
- [x] 5.13 chain safety 继续禁止跨 batch 循环。
- [x] 5.14 增加测试：一个 blocked step 创建两个 child units。
- [x] 5.15 增加测试：两个 child units 属于同一个 push contact batch。
- [x] 5.16 确认 `TryResolvePushContacts()` 按 resolved subject key 去重，而不是按 contact 数量生成 child。
- [x] 5.17 修正 pending sibling child 创建，使校验过滤和实际创建使用同一组 child requests。
- [x] 5.18 增加测试：两个 contacts 命中同一个 downstream connected body 的两个 members 时只创建一个 child unit。
- [x] 5.19 增加测试：两个 sibling 分支汇合到同一个 downstream subject 时不报 push chain cycle。

## 6. Batch 完成语义
- [x] 6.1 区分 child unit completion、batch completion 和 owner completion。
- [x] 6.2 单个 child 成功不直接完成整个 pending state。
- [x] 6.3 batch 等待所有 child units 完成。
- [x] 6.4 所有 child units 成功后 batch 成功。
- [x] 6.5 batch 成功后 parent unit 才继续现有 pending flow。
- [x] 6.6 任一 child unit 失败后 batch 失败。
- [x] 6.7 batch 失败后 parent unit 不按 unblocked 成功 retry。
- [x] 6.8 batch 失败 reason 稳定可测。
- [x] 6.9 增加测试：两个 child 都成功后 parent 继续。
- [x] 6.10 增加测试：一个 child 失败导致 batch 失败。
- [x] 6.11 增加测试：child 成功不会单独报告 owner action 成功。
- [x] 6.12 移除 pending 的 push 传播层数失败条件。
- [x] 6.13 确认 raw contact 数量和 subject member 数量不成为传播限制。
- [x] 6.14 增加测试：大型 downstream connected body 作为一个 child subject 原子移动。
- [x] 6.15 增加测试：真实 ancestor subject revisit 仍稳定失败为 push chain cycle。

## 7. Arbitration / Commit 边界
- [x] 7.1 确认 child units 仍通过统一 arbiter 生成 claims。
- [x] 7.2 确认 batch 不把 child subject members 加进 parent claims。
- [x] 7.3 确认 batch 不把 child subject members 加进 parent move plan。
- [x] 7.4 确认 accepted child unit 只提交自己的 subject/body。
- [x] 7.5 确认 target cell conflict 仍由 claim arbitration 裁决。
- [x] 7.6 增加测试：两个 child claims 不冲突时可同 tick 成功。
- [x] 7.7 增加测试：两个 child claims 冲突时按 existing conflict policy 处理。
- [x] 7.8 增加测试：parent action 不提交 child body。

## 8. 数据驱动边界
- [x] 8.1 确认 batch 创建不依赖 action name。
- [x] 8.2 确认 batch 创建不依赖 entity name。
- [x] 8.3 确认 batch 创建不通过 tag 反推 hidden behavior。
- [x] 8.4 确认 `ActionSpec` 仍只提供 blocked policy、handoff spec、handoff subject policy 等静态策略。
- [x] 8.5 增加测试：两个不同 `ActionSpecId` 使用相同 policy 时 push contact 行为一致。
- [x] 8.6 增加测试：修改 handoff spec id 只改变 child spec，不改 batch 管线。

## 9. 验证
- [x] 9.1 运行 `openspec validate fix-multi-contact-push-propagation --strict --no-interactive`。
- [x] 9.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 9.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 9.4 运行 Unity TestFramework EditMode，覆盖单 blocker 兼容。
- [x] 9.5 运行 Unity TestFramework EditMode，覆盖 contact set。
- [x] 9.6 运行 Unity TestFramework EditMode，覆盖 multi-child push contact batch。
- [x] 9.7 运行 Unity TestFramework EditMode，覆盖 batch failure。
- [x] 9.8 运行 Unity TestFramework EditMode，覆盖 parent/child commit 边界。
- [x] 9.9 运行 Unity TestFramework EditMode，覆盖 subject 去重、sibling 汇合和真实 cycle。
- [x] 9.10 运行 Unity TestFramework EditMode，覆盖大型 downstream subject 仍作为一个 child subject 原子移动。
- [ ] 9.11 用户手动 Play Mode 验证玩家推动 connected body 前沿多个目标。
- [ ] 9.12 用户手动 Play Mode 验证单 blocker push 兼容。
- [ ] 9.13 用户手动 Play Mode 验证双客户端 WorldDelta 最终一致。
