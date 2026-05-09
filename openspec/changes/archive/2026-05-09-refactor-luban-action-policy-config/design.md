## Context
现有实体 archetype、world spawn、player spawn rule、port connector 已经通过 Luban 从 Excel 导出。普通 action 行为还停留在 C# `ActionSpecRegistry.CreateDefault()`，并且 push handoff 派生 spec 仍由规则函数按 connected body 状态写死。

现在的问题不是缺少更多规则分支，而是第一层还没收口。如果继续在规则层加 action 名判断，后续 runtime effect、component 组合、port body、机制推动和技能效果会全部挤进同一批 if/else。

## This Change Is The First-Layer Stop Line
本 change 是第一层 ActionPolicy 数据驱动的收尾，不是新的长期地基工程。

完成后，第一层只保留：

- `ActionSpecId` 查策略。
- `ActionSpec` 描述普通行为策略。
- Luban/Excel 提供正式 action policy 数据。
- 规则层执行通用仲裁、pending、planning、commit。

完成后，不再继续以“数据驱动第一层”为理由扩 core action 架构。后续普通玩法必须优先进入：

- runtime effect
- component result
- final component state
- action arbitration
- WorldDelta 同步验证

## Goals
- Excel 作为普通 action policy 的正式数据源。
- Luban 生成 provider，运行时通过 mapper 构造 `ActionSpecRegistry`。
- `ActionSpec` 成为高聚合策略对象。
- handoff 派生 spec 和 subject 选择来自 `ActionSpec` handoff policy。
- 删除 `WorldActionKind` legacy 分类。
- 测试覆盖“改配置改变行为”，不是“改 C# 分支改变行为”。

## Non-Goals
- 不实现完整技能系统。
- 不实现 runtime effect / final component state 新阶段。
- 不引入 `CompositeEntity`。
- 不把 `WorldTag` 变成隐式行为脚本。
- 不保留 `WorldActionKind` legacy 行为分类。
- 不做 Unity Player build。

## High Cohesion: ActionPolicy 必须聚合的内容
这些字段共同定义一个普通 action 的玩法语义，必须放进 action policy 数据，而不是散落在 resolver / arbiter / pending / queue：

- identity：`spec_id`
- primitive：Move / Spawn / Remove
- source：Player / Auto / Mechanism / Debug / Handoff
- priority：默认仲裁优先级
- tags：source tag、required tags、blocked tags、ability tag
- target rule：目标坐标、方向来源、任意坐标
- blocked policy：Reject / StartPushIfPushable / BounceIfBouncable
- handoff policy：是否派生 action、派生 spec、派生 subject policy、链式限制
- conflict policy：目标格独占、无冲突
- interrupt policy：高优先级打断、不可打断
- merge policy：同 claim 合并
- subject policy：HitEntity / ConnectedBodyIfAny
- plan rule：MoveBody / SpawnEntity / RemoveEntity
- commit rule：设置方向、自动移动 tick 等提交副作用
- cost policy：默认 cost ticks 或 cost 来源

## Low Coupling: 运行时模块只消费 policy
这些模块是稳定执行机制，不应该知道具体 action 名：

- `ActionRequestAdapter`：把 legacy 输入或网络输入转成 `ActionRequest`，只查 `ActionSpecId`。
- `ActionArbiter`：根据 `ActionSpec` 和 final component/tag/world state 生成 accepted/rejected/derived。
- `SubjectResolver` / body resolver：根据 entry entity + subject policy 得出 subject。
- `BodyCapabilityResolver`：只判断 push entry、body movement、external blocker，不选择 action 名。
- `PendingRuleStateStore`：保存 action unit、owner、derived、retry、subject，不默认推断 `"player_push"`。
- `RulePlanner`：只把 accepted action claims 变成 commit proposal。
- `Commit` / execution：只应用 proposal，不决定普通行为策略。

## Must Do
本 change 必须完成这些内容：

1. 在 Luban schema 中增加 action policy 表结构。
2. 用 Excel 录入当前所有已存在普通 action 的策略。
3. 跑通 Luban 导出，生成 JSON 和 provider。
4. 增加 Luban DTO 到规则层 `ActionSpec` 的 mapper。
5. 让正式入口能注入 Luban-backed `ActionSpecRegistry`。
6. 给 `ActionSpec` 增加 handoff policy。
7. 移除 `ResolveHandoffSubject()` 中的 `"player_push"` / `"connected_body_move"` 选择。
8. 移除 pending state 默认 `"player_push"`。
9. 删除 `WorldActionKind`，让所有 `WorldAction` 都显式携带 `ActionSpecId`。
10. 增加配置驱动测试和 handoff policy 测试。

## Must Not Do
本 change 不允许把范围扩成这些内容：

- 不做新技能系统。
- 不做 buff/debuff 完整模型。
- 不做 final component state 的新 proposal 内容。
- 不做 CompositeEntity。
- 不为某个新玩法继续加规则层 action 名判断。
- 不用 tag 组合反向推断 hidden behavior。

## Decisions
- Decision: 新增 Luban action policy 表，而不是继续扩大 C# 默认 registry。
  - Reason: 行为策略要能通过 Excel 调整，并与实体配置使用同一正式数据管线。
- Decision: handoff policy 是 `ActionSpec` 的显式字段或子结构。
  - Reason: `ResolveHandoffSubject()` 目前按 connected body 写死 `"player_push"` / `"connected_body_move"`，这是当前最明显的数据驱动缺口。
- Decision: 保留 `ActionSpecRegistry.Default` 作为测试 fallback 或 bootstrap，但正式运行时必须可以注入 Luban provider registry。
  - Reason: 单元测试需要轻量构造，但不能让 fallback 遮住正式数据源问题。
- Decision: 删除 `WorldActionKind` legacy 分类，不再保留旧适配层。
  - Reason: 所有行为都应通过 `ActionSpecId` 查 policy，保留 legacy kind 会继续制造第二套分类入口。

## Risks / Trade-offs
- Risk: Excel 字段过多后难维护。
  - Mitigation: 字段按 policy 子结构分组，先只迁移当前已有枚举，不引入脚本式表达式。
- Risk: Luban 生成类型污染 Shared 规则层。
  - Mitigation: 增加 provider/mapper，把 Luban DTO 映射成规则层 `ActionSpec`。
- Risk: 测试 fallback 与正式数据源行为不一致。
  - Mitigation: 增加导出数据加载测试和 parity 测试，验证关键 action 的 Excel 数据与现有期望一致。

## Migration Plan
1. 扩展 Luban schema 和 Excel：加入 action policy 表。
2. 导出 JSON 和生成代码。
3. 增加 `ActionSpecProvider` / mapper，把 Luban 数据映射成 `ActionSpecRegistry`。
4. 让 server/client 运行时注入 Luban registry。
5. 把 handoff spec 选择迁移到 policy 字段。
6. 删除 legacy `WorldActionKind` 和默认 registry 的内置 action 表。
7. 补 Unity EditMode、Shared/server 验证和手动端到端说明。

## Done Means
- `ActionSpecRegistry.CreateDefault()` 不再是正式 action policy 来源。
- `ResolveHandoffSubject()` 不再写死 action id。
- `PendingRuleStateStore.AddHandoffActionState()` 不再默认 `"player_push"`。
- `WorldActionKind` 不再存在于运行时 action 模型。
- 新增一个普通 move-like action 可通过 Excel + 测试完成。
- 之后如果要做涌现玩法，进入 runtime final component state 方案，而不是继续扩大第一层。

## Open Questions
- `connected_body_move` 是否保留为兼容 spec，还是迁移为 `player_push` / `mechanism_push` 的 handoff policy 目标后逐步删除？
- cost policy 第一阶段是否只保留固定 ticks，还是同时支持从 runtime params 覆盖？
- Excel 表是否拆成一张 `action_spec.xlsx`，还是拆为 `action_spec.xlsx` + `action_handoff_policy.xlsx`？
