# Design: 原子事务与涌现运动边界

## Context

DG 的规则层已经有 action unit、claim arbitration、planning、commit、pending handoff 和 connected body view。近期 push 调试中出现两类现象：

- 多个分支最终命中同一个 downstream connected body。
- 闭环 port 结构让 push 反馈回已经参与过的 subject。

这两类现象都不应该通过一次 pending push chain 无限展开来解决。前者是 convergence，后者是 tick-based emergent motion 的边界问题。

## Goals

- 定义一次 action / tick 原子事务的最大消费边界。
- 支持同 tick 多 push 输入对同一 subject 的叠加或合并。
- 阻止同 tick feedback push 在闭环中无限放大。
- 让闭环装置通过 `tick + cost` 的 deferred output 持续输出，而不是让 pending 求完整链条。
- 保留现有 action-unit、claim、plan、commit 模型。
- 将 push propagation 从 parent-child pending 语义迁移到显式输出事件。
- 保证新输出结构由策略字段驱动，不依赖 action 名字、entity 名字或 tag 组合。

## Non-Goals

- 不设计完整打断优先级系统。
- 不实现持久 `CompositeEntity`。
- 不把闭环装置升级为一次全局图求解。
- 不要求 Unity Player build 作为验证。
- 不在 proposal 阶段修改运行时代码。

## Decisions

### Decision: 单 tick 内同一 subject 最多消费一次

同一 tick 内多个 push 可以命中同一个 subject，但该 subject 只能消费一次合并后的输入。这样支持合力，又避免闭环反馈在同一 tick 内反复消费。

### Decision: downstream 输出最早 tick + cost 生效

一个 subject 消费 push 后产生的 downstream push 输出不得在同一事务中立即再次消费。输出应记录为结构化 deferred output，并在 `ready tick >= current tick + cost` 时作为新的 action input 重新进入统一管线。

### Decision: convergence 不是 cycle

完全相同 subject 在同一事务中被重复命中表示多路径收敛，应跳过或合并；不同 subject 但共享部分 entity 表示 action-unit 所有权不清，应保持失败或冲突。

### Decision: 直接移除 push 对 Pending 的依赖

Push propagation 不再创建 parent-child pending handoff，也不等待 downstream push action 的成功或失败。当前 push 链条不能作为未来等待型 action 的通用底座保留，因为闭环和涌现装置会让链条复杂度不可维护。

如果未来确实出现 parent 必须等待 child result 的 action，需要另起 proposal，单独定义：

- child 数量上限。
- 等待终止条件。
- cycle / convergence / partial overlap 规则。
- 超时或取消规则。
- owner result 聚合规则。

它不能复用本次要移除的 push pending chain。

### Decision: deferred output 是策略数据，不是硬编码机制

Deferred output 必须由 action policy / output policy 明确描述。最小字段包括 source subject、target subject、intent、direction 或 vector、ready tick、cost、dedupe key 和 causality id。规则层不得根据 `ActionSpecId` 字符串、entity 名字、tag 组合或类似 `mechanism_push` 的特殊名字推断输出行为。

### Decision: 涌动增幅机器第一阶段只做周期输出

“涌动增幅 push 机器”在第一阶段没有 strength 数据模型时，只能表现为周期输出装置：外部输入触发一次消费，feedback 回到同一 subject 被本 tick 消费边界截断，有外部 output 时生成 `ready tick = current tick + a` 的 deferred output。它不在同 tick 内放大，也不跨 tick 增加强度。

未来如果需要成为真正的 push 增幅器，必须新增显式 strength policy，定义 feedback strength 是否跨 tick 保留、同 tick 多输入如何 merge、output strength 如何叠加/衰减/封顶，以及 dedupe key / causality id 如何防止同一 cause 重复增殖。

## Risks / Trade-offs

- 风险：如果只跳过 duplicate subject，闭环输出可能丢失。
  - 缓解：spec 明确后续输出必须变成 future tick 的 deferred output，而不是静默吞掉所有效果。
- 风险：同 tick push 叠加强度尚无完整数据模型。
  - 缓解：第一阶段可先定义消费边界和重复防护，强度模型作为显式后续扩展。
- 风险：现有 `fix-multi-contact-push-propagation` 正在修改相同区域。
  - 缓解：本 change 作为更底层的 proposal，实施时先对齐 active change 的已完成行为，再拆最小任务。
- 风险：deferred output 被实现成新的硬编码 push 分支。
  - 缓解：spec 要求字段来自显式 policy，并增加测试覆盖不同 `ActionSpecId` 共享相同行为策略时结果一致。

## Migration Plan

1. 先补测试描述当前 failure：闭环 feedback 不应在同一 pending chain 无限展开。
2. 增加 subject/tick 消费记录或等价 guard，保证同 subject 同 tick 最多消费一次。
3. 将 duplicate subject 与 unsafe overlap 区分。
4. 新增结构化 deferred output 数据，并让 blocked push 的继续输出写入该结构。
5. 将 ready deferred output 放回 world action queue。
6. 从 push propagation 路径删除 pending child handoff 依赖，而不是保留为备用链条。
7. 若删除后 `PendingRuleStates` 没有非 push 调用方，则删除或隔离该系统；若仍有调用方，必须证明调用方不是 push propagation。
8. 保留现有 manual Play Mode / 双客户端验证作为端到端验收。

## Resolved Decisions

### Decision: 第一阶段不引入 push strength 数据模型

第一阶段只定义 action 的原子消费边界、subject 去重、claim 仲裁和输出事件边界。push strength、叠加倍率、衰减和能量类规则属于后续 emergent motion 扩展，不阻塞本 change。

### Decision: 闭环输出必须结构化

闭环输出不得依赖 action 名字、entity 名字或 tag 组合推断行为。最小输出描述应包含 source subject、target subject、intent、direction 或 vector、ready tick、cost、dedupe key 和 causality id；weight / strength 可作为保留字段进入后续强度扩展。

### Decision: 无外部输出的闭环成功完成但可诊断

无外部输出的闭环事务应成功完成，不产生后续 action input。调试层应能记录 `bounded/no-output` 诊断。若闭环存在有效外部输出，则不得记录为 `bounded/no-output`，而应记录为 `bounded/deferred-output` 或等价诊断，并产生 deferred output。
