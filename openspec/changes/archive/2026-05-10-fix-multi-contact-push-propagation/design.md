# Design: Multi-Contact Push Propagation

## Context
当前 action 管线已经把普通行为收敛到 `ActionSpec -> ActionRequest -> ActionArbiter -> RulePlanner -> Commit`，pending push 也用 parent / derived unit 表达链式传播。

当前缺口更窄：`TryBuildMoveClaims()` 已经能为 connected body 生成多个 `BodyMove` claims，但 `FindExternalBlocking()` 只返回第一个 external blocker，导致 `ResolveBlocked()` 和 `ResolvePushableBlock()` 只处理单个 contact。connected body 的多个前沿格如果同时接触多个外部 pushable subjects，只有第一个会派生 child push。

本 change 不把问题扩大成完整事务系统。目标是修复 push 实现层的 multi-contact 缺口，并给同一次 blocked step 派生出的多个 child units 增加最小 batch 完成语义。

## Goals
- 收集一个 action unit 的所有 external push contacts，而不是第一个 blocker。
- contact 收集基于本次 `BodyMove` claims 的目标格，不扫描无关区域。
- 一个 blocked step 可以创建多个 child push units。
- child units 继续独立进入 claim arbitration 和 commit。
- 同一 push contact batch 中所有 child 成功后，parent 才继续现有 pending flow。
- 同一 push contact batch 中任一 child 失败时，batch 失败。
- 保持 action 名无关和数据驱动边界。

## Non-Goals
- 不做完整 transaction system。
- 不做任意事件树、脚本节点图或通用行为树。
- 不实现 push 后反弹力、反向传力或结果驱动 reaction action。
- 不改变 Luban action policy 数据源的第一层目标。
- 不让 client UI 或 Unity 镜像裁决 push batch。

## Layering
- `ActionSpec`：描述 action unit 的静态策略，例如 blocked policy、handoff spec、handoff subject policy、conflict/merge/commit。
- `Contact Set`：由本次 `BodyMove` claims 的目标格收集 external blocking contact 事实。
- `Arbiter`：根据 blocked policy 将 contacts 解释为 handoff targets，并按 resolved downstream subject 去重后派生 child push units，或按现有逻辑 reject / bounce。
- `Pending`：保存 parent-child 关系、batch 归属、ready tick、subject safety、timeout 和 batch completion。
- `Arbitration`：只裁决 action units 产生的 claims 是否共存。
- `Commit`：只提交 accepted units/plans，不提交未被自身 action unit 接受的下游 body。
- `Tag`：只作为能力、状态、免疫、阻挡等规则输入，不表达 batch 结构。

## Proposed Model

### ExternalPushContact
新增或等价表达 contact 数据：
- `BlockerEntityId`
- `SourceEntityId`
- `FromCoord`
- `ToCoord`
- `ClaimActionId`

contact set 规则：
- 只遍历 `ActionClaimKind.BodyMove`。
- 查询每个 claim 的 `ToCoord`。
- 排除当前 moving body 自身成员。
- 只收集 blocking entities。
- 保持 deterministic ordering。

contact set 不决定 push 结构，也不按 connected body 去重。多个 contacts 命中同一个下游 connected body 的多个 members 时，contact set 仍保留多个命中事实。

### Handoff Target Resolution
`StartPushIfPushable` 消费 contact set 时才解释 push 语义：
- 每个 contact 先检查 blocker 是否可作为 push entry。
- 每个 contact 按 parent `ActionSpec.Handoff` 解析 handoff spec 和 handoff subject。
- handoff subject 使用稳定 key 去重，key 基于排序后的 subject entity ids。
- 同一个下游 connected body 只生成一个 child unit。
- 不同下游 subjects 才生成多个 child units。
- contact 数量不等于 child unit 数量。

### PushContactBatch
pending 层增加最小 batch 语义：
- `BatchId`
- `ParentUnitId`
- `ChildUnitIds`
- `Status`
- `CreatedTick`

第一阶段只需要固定语义：
- 全部 child 成功后 batch 成功。
- 任一 child 失败后 batch 失败。
- batch 成功后 parent 才按现有 pending flow 继续。
- child 成功本身不报告 owner action 成功。

pending 层不重新解释 contact。它只接收已经解析并去重后的 child unit requests，并保证：
- 同一 state 内不会创建重复 ready subject。
- 真正回到 ancestor subject 时稳定失败为 chain cycle。
- 同一 batch 的 sibling child 创建使用与校验相同的过滤结果。
- 不根据 push 传播层数拒绝行为；行为单元自身保持原子，传播只受循环、重复 subject、timeout 和普通仲裁/提交结果约束。

这不是通用事务系统，也不支持回滚、脚本图、任意依赖拓扑或 reaction 链。

## Pipeline
1. ready unit 进入 arbiter。
2. arbiter 解析 action spec、subject/body、direction 和 move claims。
3. contact set 遍历本次 `BodyMove` claims 的目标格。
4. 如果没有 external contacts，action unit 进入普通 claim arbitration。
5. 如果存在 external contacts 且 `BlockedPolicy = StartPushIfPushable`，为 pushable contacts 解析 handoff subject。
6. pending 为同一次 blocked step 创建 push contact batch。
7. pending 为每个 contact 创建独立 child unit。
8. child units 按现有 ready action unit 路径进入 arbitration / planning / commit。
9. 所有 child 成功后，batch 成功，parent 才继续现有 pending flow。
10. 任一 child 失败时，batch 失败，parent 不按 cleared path retry。

## Decisions
- Decision: 不使用 transaction system 作为本 change 的主要模型。
  - Reason: 当前已确认根因是 single-blocker 查询和单 child pending 假设，先修复 multi-contact push 即可。
- Decision: contact set 来源于 move claims 的目标格。
  - Reason: 这正好对应“本次预期要占用哪里”，避免扫描无关格子。
- Decision: child units 仍独立 claims/arbitration/commit。
  - Reason: 防止回到父 action 大原子提交下游 body 的旧复杂度。
- Decision: batch 完成语义固定为全部成功或任一失败。
  - Reason: 当前 push 传播需要整批一致，`PartialAllowed` 不是本 change 目标。
- Decision: contact 到 child 的去重发生在 handoff target resolution，不发生在 contact set。
  - Reason: contact set 只描述命中事实；下游 connected body subject 是 push handoff 解释结果。
- Decision: pending safety 不知道“多个 contact 命中同一个 body”的业务规则。
  - Reason: pending 应只管理 child unit 生命周期、subject safety、depth、batch result 和 owner result。
- Decision: 不把 push contact batch 提升成通用 transaction batch。
  - Reason: 当前只有 push 有 contact fanout、handoff subject 和 all-success batch 语义；等其他 action 出现同类需求再抽象。
- Decision: 不新增 tag 表达 batch 行为。
  - Reason: tag 只表达实体事实，不能替代 pending 运行时结构。

## Risks
- batch 完成条件容易和现有 `MarkUnitAccepted()` 的“一个 unit 成功即 state completed”冲突。
  - Mitigation: 只在 pending 层区分 child unit completion、batch completion 和 owner completion。
- 同 tick 多 child units 可能与现有 active-chain duplicate 检查冲突。
  - Mitigation: 允许同一 batch 内不同 subjects 并列，重复 downstream subject 在 handoff target resolution 或 pending safety 中跳过，但真正循环仍失败。
- pending chain metadata 可能混合 ancestor、active subject 和 involved entity 三种含义。
  - Mitigation: 实现阶段优先收窄职责，让 duplicate subject guard 与 cycle guard 可测试地区分，并避免把传播层数当作玩法失败条件。
- 文档继续使用 transaction/tree 术语会把实现带偏。
  - Mitigation: proposal、design、tasks 和 spec delta 统一改为 multi-contact push / push contact batch。

## Validation
- `openspec validate fix-multi-contact-push-propagation --strict --no-interactive`。
- `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- Unity TestFramework EditMode：
  - 单 blocker push 行为保持兼容。
  - connected body 前沿两个 contacts 被同一 blocked step 收集。
  - 一个 blocked step 创建多个 child units。
  - 多个 child units claims 不冲突时可以同 tick 成功。
  - 任一 child unit 失败时 push contact batch 失败。
  - 下游 body 不进入父 action member commit。
- 手动 Play Mode：
  - 玩家推动 connected body 时，多个前沿 external contacts 都进入推动结果。
  - 单 blocker push 兼容。
  - 双客户端 WorldDelta 最终一致。
