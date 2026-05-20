# 行为层结构文档入口

## 目的

这个目录整理 DG 行为层，也就是当前口头说的“小 GAS”。

它不新增需求，不替代 OpenSpec，也不声明某个变更已经完成。它只把现有目标架构、数据链路、工作流和代码边界整理到同一套语言里，方便后续继续拆 rotate、push、input、presentation 和 runtime effect。

## 阅读顺序

1. `behavior-layer-architecture.md`
   - 先看这个。它说明行为层的核心主语、分层边界、哪些东西属于小 GAS，哪些不属于。
2. `data-flow.md`
   - 看配置、输入、ActionSpec、ActionRequest、ActionFact、PresentationFact、WorldDelta 如何串起来。
3. `runtime-workflows.md`
   - 看一次行为从提交到结算、跨 tick、deferred output、客户端播放的工作流。
4. `module-map.md`
   - 查代码应该放在哪里，以及新增能力时不要碰哪些边界。

## 现有相关文档

- `docs/source-layout.md`：代码目录放置规则。
- `docs/gamecore-extension-boundaries.md`：GameCore 扩展边界。
- `docs/aa/atomic-transaction-and-emergent-motion.md`：原子事务和 deferred push 边界。
- `docs/aa/config-data-governance.md`：Luban 配表治理目标。
- `docs/Goal/Acion/Logic/timed-action-unit-logic.md`：行为实例、状态机、预约和 ActionFact 的目标口径。
- `docs/Goal/Acion/Animation/presentation-group-playback.md`：客户端表现 group 播放目标。
- `docs/Goal/Acion/special/push/current-push-business-logic.md`：push 当前业务语义。
- `docs/Goal/Acion/special/rotate/rotate-architecture.md`：rotate pivot 目标语义。

## 当前判断

DG 行为层已经不是单纯 move 系统。

它现在已经有：

- 数据驱动 `ActionSpec`。
- 输入到 action request 的适配层。
- targeting、subject、gating、strategy、claim、planning、commit 等阶段。
- deferred action queue。
- runtime component result。
- timed action 过渡实现。
- 服务端事实到客户端表现事实的同步链路。
- Unity TestFramework 和 server verification 的基本测试体系。

但行为层还没有完全收敛为最终形态：

- `ActionBehaviorInstance` 仍是目标口径，当前代码里 `TimedActionUnit` 还是过渡实现。
- `ActionFact` 和 `PresentationFact` 的边界还在迁移中。
- rotate sweep 还需要从固定 90 样本语义升级为 `CostTicks` 弧段语义。
- 客户端 group playback 已有方向，但还需要继续收口普通动画覆盖、连接线 ownership 和最终对齐。

## 不要混淆

- 小 GAS 不是 Unity 客户端动画系统。
- 小 GAS 不是 Fantasy Handler。
- 小 GAS 不是 Luban 表本身。
- 小 GAS 不是沙盒 debug layout。
- 小 GAS 不是完整技能脚本 VM。

小 GAS 是服务端权威行为层：用配置和明确策略描述行为，用权威世界状态裁决行为，用有限事务提交世界变化，用事实把复杂行为同步给客户端。
