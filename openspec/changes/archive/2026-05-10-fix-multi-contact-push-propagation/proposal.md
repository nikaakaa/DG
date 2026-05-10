# Change: 修复 multi-contact push propagation

## Why
当前 connected body push 的主要问题不是缺少完整事务系统，而是实现层在 move claims 已经生成多个目标格后，只查找并处理第一个 external blocker。

`BodyCapabilityResolver.FindExternalBlocking()` 当前遇到第一个 blocking entity 就返回，`ActionSpecs.ProcessMoveRequest()` 随后只对这个单 blocker 执行 `ResolveBlocked()`。因此当一个 connected body 的多个 member 在同一 tick 同时接触多个外部 pushable body 时，后续 contact 会被忽略。

这个 change 的目标是先修复 push 传播的多接触点实现缺口：收集本次 body move claims 命中的所有 external contacts，并让同一次 blocked step 可以派生多个独立 child push units。它不升级为完整事件树或通用事务系统。

## What Changes
- 将 external blocker 查询从“找到一个就返回”改为 contact set 收集。
- contact set 来源只限于本次 action unit 产生的 `BodyMove` claims 的目标格。
- contact set 排除当前 moving body 自身成员，但保留本次 claims 命中的 contact 事实。
- `StartPushIfPushable` 将 contacts 解释为 handoff targets，并按 resolved downstream subject 去重。
- `StartPushIfPushable` 在一个 blocked step 中可以为多个不同 downstream subjects 创建多个 child units。
- 多个 child units 保持独立 action unit：各自生成 claims、各自参与 arbitration、各自提交自己的 subject/body。
- pending 层增加最小 push contact batch 语义：同一批 child 全部成功后，parent 才按现有 pending flow 继续；任一 child 失败则该批 push 失败。
- pending 层只做运行时安全：防重复 subject、防真正循环、处理 timeout、聚合 batch 和 owner result。
- 保持 action 行为数据驱动：`ActionSpec` 仍只提供 blocked policy、handoff spec、handoff subject policy、conflict/merge/commit 等静态策略；规则层不得根据 action 名、entity 名或 tag 组合硬编码 fanout 行为。

## Non-Goals
- 不重构完整事务系统。
- 不实现完整事件树或任意脚本化 action graph。
- 不实现 push 后反弹力、反向传力或结果驱动反应链；这些后续单独设计。
- 不把普通 push chain 重新合并成一个大原子 action commit。
- 不把 connected body 与下游 body 合并为同一个 subject。
- 不把 contact set、`CanPushEntry`、contact 到 handoff subject 的解释抽象为通用事务系统。
- 不引入 Unity Player build 验证。

## Impact
- Affected specs: `claim-driven-action-arbitration`, `data-driven-runtime-actions`
- Affected code: `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`, `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`, `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`, `Shared/DG.GameCore/Rules/Planning/BodyCapabilityResolver.cs`, Unity EditMode tests, server verification tests
