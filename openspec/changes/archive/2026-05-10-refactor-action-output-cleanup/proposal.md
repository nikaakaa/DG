# Change: 清理 action output 配置与 Pending 残留边界

## Why

`define-atomic-transaction-boundary` 已经把 push 传播改成原子 action 输出 deferred action，但当前代码表面仍有两个不干净的边界：`PushOnEnterComponent` 默认输出 spec 在组件应用层硬编码为 `"mechanism_push"`，并且普通 action / deferred 主链路的 API 仍显式携带 `PendingRuleStateStore`，容易把 push propagation 又拉回 parent-child pending 语义。

## What Changes

- 将进入推动组件的输出 action spec、输出 cost 等数据放到 Luban Excel 源表中，并用 Python 修改 Excel；`ComponentApplicationRegistry` 不再硬编码普通行为 spec id。
- 收窄 Pending 的职责：push continuation 和 deferred output 主链路不得依赖 `PendingRuleStateStore`、`PendingActionState` 或 child-unit API。
- 删除旧 fallback 数据或将其隔离到非正式测试边界，正式运行和覆盖测试统一使用 Luban 生成数据。
- 保留未来真正需要“父 action 等待子 action 结果”的能力入口，但要求它作为独立 proposal 重新定义有限 child 数、终止、取消、循环、收敛和 owner result 聚合规则。
- 补充自动测试和手动验证边界，证明清理后现有 push、PushOnEnter、deferred output 和 sandbox 行为保持一致。

## Impact

- Affected specs: `data-driven-runtime-actions`, `shared-gamecore-entity-rules`
- Affected code: `Config/Luban/Defines/gamecore.xml`, `Config/Luban/Datas/gamecore/*.xlsx`, `Shared/DG.GameCore/Config/ComponentApplicationRegistry.cs`, Luban entity/output config mapping, `PushOnEnterComponent`, `StateDrivenRules.cs`, `ActionSpecs.cs`, `WorldActions.cs`, server tick runner, Unity EditMode tests, server authoritative verification
- Dependencies: should be applied after or together with `define-atomic-transaction-boundary`
