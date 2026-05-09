# Change: 收尾第一层 ActionPolicy 数据驱动

## Why
当前 action 行为已经收敛到 `ActionSpec`，但第一层仍有三个会继续制造 if/else 的硬编码点：

- `ActionSpecRegistry.CreateDefault()` 把正式 action policy 写在 C# 里。
- `ResolveHandoffSubject()` 仍按 connected body 状态写死 `"player_push"` / `"connected_body_move"`。
- `WorldActionKind` 仍被部分入口当成普通行为分类，`EnqueueConfiguredMove()` 还伪装成 `MechanismPush`。

这个 change 的目的不是继续扩第一层，而是把第一层收尾：普通 action 策略改由 Luban + Excel 数据提供，规则层只消费 `ActionSpec` policy。完成后，后续涌现玩法应转入 runtime final component state / runtime effect / action arbitration 组合层，而不是继续在第一层补硬编码。

## What Changes
- 新增 Luban action policy 配置，Excel 作为正式数据源，导出 JSON / C# provider。
- 把当前 C# 默认 action 列表迁移到 `action_spec.xlsx`，覆盖现有 `player_move`、`auto_move`、`mechanism_push`、`debug_move`、`debug_spawn`、`debug_remove`、`player_push`、`configured_wind_push`、`connected_body_move`。
- 增加 `handoff policy` 数据，表达派生 spec、派生 subject policy、链式限制和失败行为。
- 让正式 server/client 运行时通过 Luban provider 构造 `ActionSpecRegistry`。
- 移除 handoff 派生 spec 的 action 名硬编码。
- 移除 `WorldActionKind` legacy 分类，新普通行为和现有便捷入口都必须通过 `ActionSpecId` + policy 数据进入。
- 保留测试 fallback，但 fallback 只能用于单元测试或配置缺失诊断，不能成为正式运行路径。

## Explicit Scope
本 change 只做第一层 ActionPolicy 数据驱动收尾：

1. `ActionSpec` 数据源从 C# 默认 registry 迁移到 Luban/Excel。
2. `handoff policy` 从规则代码硬编码迁移到 action policy 数据。
3. 删除 `WorldActionKind` legacy 分类。

## Non-Goals
- 不实现完整技能系统。
- 不实现 runtime effect / final component state 的新能力层。
- 不引入 `CompositeEntity`。
- 不把 `WorldTag` 做成隐式行为脚本。
- 不引入脚本式 Excel 表达式。
- 不继续扩展第一层架构来解决所有未来玩法。
- 不做 Unity Player build。

## Completion Line
完成本 change 后，第一层应停止继续扩张。验收标准是：

- 新增一个普通 move-like 行为只需要新增 Excel action policy 行和测试，不需要改核心 arbiter / planner / commit。
- 修改 push handoff 派生 spec 或 subject 只改 Excel policy，不改 `ResolveHandoffSubject()`。
- 核心规则层不再按 `ActionSpecId` 字符串、entity 名或 tag 组合推断普通行为。
- `WorldActionKind` 不再存在于运行时 action 模型。
- 后续新增涌现玩法进入 runtime final component state / runtime effect / action arbitration 组合层验证。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `data-driven-runtime-actions`, `claim-driven-action-arbitration`
- Affected code: `Config/Luban/Defines/gamecore.xml`, `Config/Luban/Datas/gamecore/*.xlsx`, `Config/Luban/Run.ps1`, `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`, `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`, `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`, `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`, Unity EditMode tests, server verification tests
- Validation: OpenSpec strict validation, Luban export, Shared GameCore build, server authoritative move verification, Unity TestFramework EditMode；端到端仍由用户手动 Play Mode / 双客户端验证
