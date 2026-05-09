# Change: 收紧 port linked body 底层语义

## Why
当前 `PortConnectorComponent`、`PortConnectionSystem`、`BodyResolver` 已经能把 port 相邻实体解析成 connected body，但底层语义还需要更明确：port 连接后 component 是否传播、pushable 如何作为入口、linked body 如何聚合移动能力、以及后续 cache/dirty 边界如何预留。

如果不先把这些规则固定，后续轮胎、车、模块装配、机关推动和普通 push 会继续混在一起，容易把 port graph 误做成父子树或持久 `CompositeBodyEntity`。

## What Changes
- 明确 port linked body 是运行时图连通视图，不是持久实体树。
- 明确 linked body 内 member component 不自动传播。
- 明确 `PushableComponent` 是 push entry 能力，不是整个 body 的自动共享能力。
- 增加 body-level capability 聚合边界，用于判断 entry、movability、blocking、control。
- 明确 linked body action 只能 all-or-nothing commit，不能提交部分 member 成功。
- 预留 `PortGraphCache` / `ConnectedBodyCache` / dirty-driven rebuild 边界，但本变更不要求立即实现持久 cache。

## Impact
- Affected specs: `shared-gamecore-entity-rules`
- Affected code: `PortConnectionSystem`, `BodyResolver`, `ActionArbiter`, `RulePlanner`, `ActionSpecs`, `ConflictResolver`, Unity EditMode tests
- Non-goals: 不引入持久 `CompositeBodyEntity`，不引入 module/vehicle/wheel 语义，不修改 Fantasy 协议，不执行 Unity Player build
