## Context

当前目标不是重新设计原子事务，而是清理已落地模型周围的工程债务。现有行为已经接近目标：

- `ActionSpec` 来自 Luban action policy。
- push 被阻挡后产生 `DeferredAction`，source action 完成自身事务。
- `BodyCapabilityResolver` 只发现 external contacts。
- `ActionSpecs` 将 contacts 解释成 downstream subject 和 deferred output。
- tick runner 将 deferred output 重新入队。

仍然不干净的点：

- `ComponentApplicationRegistry` 在应用 `ComponentKind.PushOnEnter` 时写死 `"mechanism_push"`。
- `StateDrivenRuleExecutionSystem.Tick`、`ActionArbiter.ArbitrateMoves` 和若干内部函数仍把 `PendingRuleStateStore` 作为普通 action 主链路参数。
- `PendingRuleStates` 仍包含 handoff child-unit 能力，容易被误认为 push propagation 的默认实现。

## Goals

- 普通进入推动输出由配置数据表达，不由组件应用代码写死 action id。
- action/deferred 主链路可以在没有 pending store 的情况下运行。
- push propagation 不再通过 pending child-unit API 表达。
- 未来需要等待子结果的 action 必须独立建模，不能复用旧 push pending 链作为隐式 fallback。
- 保持现有已覆盖行为结果不变。

## Non-Goals

- 不删除未来所有 waiting action 的可能性。
- 不在本次引入 push strength、累积强度或同 tick 增幅器策略。
- 不把 sandbox JSON 变成正式配置源。
- 不执行 Unity Player build。

## Decisions

### Decision: PushOnEnter 输出成为 Luban Excel 显式配置数据

`PushOnEnterComponent` SHALL carry output action spec and cost from Luban Excel config. 本次不接受 fallback 数据、测试默认值或先写 provider 抽象再等待源表导出的过渡方案。配置源统一为 `Config/Luban/Datas/gamecore` 下的 Excel，必要的 Excel 修改使用 Python 脚本完成，然后运行 `Config/Luban/Run.ps1` 生成代码和 JSON。

当前 `port_connector_config.xlsx` 已经使用 `config_id -> component config` 的独立表模式，PushOnEnter 应采用同样的模式新增 `push_on_enter_config.xlsx`，而不是把 component-specific 字段继续堆进 `entity_archetype.xlsx`。

推荐字段：

- `config_id`
- `output_spec_id`
- `output_cost_ticks`

约束：

- 拥有 `ComponentKind.PushOnEnter` 的 archetype MUST 有对应 `push_on_enter_config` 行。
- 没有 `ComponentKind.PushOnEnter` 的 archetype MUST NOT 依赖该表产生输出。
- `output_spec_id` MUST reference existing `ActionSpec.spec_id`。
- `output_cost_ticks` MUST be positive。

### Decision: 主 action pipeline 不接收 Pending 作为默认参数

`StateDrivenRuleExecutionSystem` 的普通 ready action/deferred output 流程 SHALL not require `PendingRuleStateStore`。如果仍需保留 pending legacy coverage，应隔离到 legacy adapter、专用测试或后续独立 waiting-action pipeline，而不是作为 `ActionArbiter` 的必选输入。

### Decision: PendingRuleStates 暂不强删，但降级为隔离遗留/未来实验边界

直接删除 `PendingRuleStates` 可能扩大风险，因为还有直接测试和 remove/cancel 逻辑引用。第一步应移除生产 push/deferred 主链路依赖；文件是否删除由后续清理根据引用归零情况决定。

## Risks / Trade-offs

- Luban schema 改动需要重新导出生成表和 JSON；如果生成工具不可用，本次实现应停止并修复导出链，不使用 fallback 绕过。
- 移除 pending 参数可能牵动较多测试 helper，需要先添加兼容 overload 再逐步替换调用点。
- 如果未来真的需要 parent waits child result，必须重新设计有限等待模型，短期不能借旧 pending 链快速接回去。

## Migration Plan

1. 使用 Python 修改 Luban XML 和 Excel，新增 `push_on_enter_config.xlsx` 及对应表定义。
2. 运行 `Config/Luban/Run.ps1`，生成 Shared Luban 表代码、生成 JSON，并同步 Unity StreamingAssets。
3. 更新 `IGameConfigProvider` / `LubanGameConfigProvider`，从 Luban 表读取 PushOnEnter 输出配置。
4. 更新 `ComponentApplicationRegistry` 从 provider 输出配置创建 `PushOnEnterComponent`。
5. 给 `StateDrivenRuleExecutionSystem` 增加不需要 pending store 的主入口，并迁移生产 runner 与普通测试。
6. 从 `ActionArbiter` push/deferred 分支移除 pending 参数。
7. 删除或隔离不再被生产路径使用的 fallback provider 数据和 pending child-unit 测试。
8. 运行 OpenSpec、Shared build、server verification、Unity EditMode。
9. 由用户手动验证 Play Mode 和双客户端同步。

## 配表能解决什么

可以通过配表解决：

- 哪些 entity 拥有 PushOnEnter 能力。
- PushOnEnter 输出哪个 `ActionSpecId`。
- 输出 cost 是多少 tick。
- conveyor、wind field、水流等 tile-like entity 复用同一个组件但输出不同行为。
- 配置缺失、spec id 无效、cost 非法这类静态数据错误。

不能只靠配表解决：

- `ComponentApplicationRegistry` 如何把生成配置转换成 `PushOnEnterComponent`，这需要 provider 和组件应用代码读取新表。
- `StateDrivenRuleExecutionSystem` 是否需要 `PendingRuleStateStore`，这是运行时 API 边界，不是数据字段能改变的。
- push 被阻挡时生成 `DeferredAction` 而不是 pending child unit，这是规则层实现语义。
- 同 tick subject 消费、deferred 入队、runner tick 调度和 WorldDelta 同步，这些是引擎规则和执行管线。

因此方案是：输出参数走 Luban 配表；执行语义仍由统一 action/deferred pipeline 实现。配表不能替代规则系统，但可以保证规则系统不从 action 名字、entity 名字或 tag 组合推断普通行为。
