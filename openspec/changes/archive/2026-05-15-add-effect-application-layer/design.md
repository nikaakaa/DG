## Context
DG 当前已经有 `RuntimeEffectStore`、`RuntimeEffectSpec`、`RuntimeEffectInstance` 和 `ComponentStateResolver`，并且规格已经要求规则层只读取 final Component/tag result。行为层也已有 `ActionSpec`、`ActionContext`、`TargetData`、`ExecutionOutput`、`CommitProposal` 的活跃重构方向。

缺口在于：Effect 还不是 action execution 的一等输出。现在 runtime effect 更像调试或外部来源状态，而不是由 action 命中目标后生成、通过 commit 接受、再进入 resolver 的权威效果实例。

## Goals
- 让 `EffectSpec` 成为静态效果定义入口。
- 让 `EffectApplication` 成为 action execution 产生的运行时效果实例。
- 让 `CommitProposal` 成为添加/移除 runtime effect、component result 和 tag result 的唯一写入入口。
- 保持 `RuntimeEffectStore` 不直接写 `GameWorld` component store。
- 保持 action arbitration、planning 和 rules 不查询 effect store 或 effect kind。
- 给客户端表现提供 effect/cue metadata，但不把客户端变成裁决者。

## Non-Goals
- 不实现完整 Ability 激活流程。
- 不实现通用 Attribute/Stat。
- 不实现 GameplayTag registry。
- 不实现复杂表达式语言、行为树、脚本 VM 或 UE GAS 对象模型。
- 不改变移动/推动仍必须走 claim、arbitration、planning、commit 的规则。

## Decisions
- Decision: `EffectSpec` 使用 Luban 独立表作为正式配置来源，第一片不使用手写内置 registry 作为主路径。
- Decision: `EffectSpec` 是静态定义，不保存运行时来源、目标、tick 或 stack 实例状态。
- Decision: tag result 第一片继续使用现有 `WorldTag`，不引入 tag id registry 或层级 GameplayTag 系统。
- Decision: `EffectApplication` 是运行时实例输入，必须携带 action context 或等价 source context、target data、target entity/body/cell、start tick、expire tick、causality 和 stack key。
- Decision: Action execution 生成 `EffectApplication[]`，再转换为 commit proposals；strategy/policy 不直接调用 `GameWorld.AddRuntimeEffect`。
- Decision: `CommitResolver` 接受 effect commit 后写入 `RuntimeEffectStore` 或 tag/component source state，并记录 dirty/journal/delta 所需 metadata。
- Decision: Effect 对 component/tag 的影响必须作为独立 contribution/source 保留；添加 effect 只添加自己的 source，移除 effect 只移除自己的 source，最终结果统一由 resolver/settlement 结算。
- Decision: `ComponentStateResolver` 仍是 final Component result 的唯一合成边界。
- Decision: 第一片支持现有 first-slice component result：Blocking、AutoMove、Pushable、PortConnector、MovementPermission，以及现有 `WorldTag` 的 Add/Remove tag result。
- Decision: `SetComponentResult` 第一片只表示 runtime component contribution，不允许一次性覆盖 final component result。
- Decision: Stack policy 第一片只支持 `ReplaceByStackKey`、`RefreshDuration`、`AllowMultiple` 和 `RejectDuplicate` 这类可测试的 deterministic 策略。
- Decision: Duration 第一片只支持 instant、timed ticks 和 infinite until explicit remove。

## Sequencing
1. 先新增 Luban `effect_spec` 及必要枚举/字段，并生成运行时 registry。
2. 再扩展 effect 数据模型和 commit proposal 语言。
3. 再把 `ApplyRuntimeEffect` action primitive 接到 `EffectApplication` 输出。
4. 再把 resolver 和 world delta metadata 接起来。
5. 最后迁移 debug apply/remove runtime effect 到同一条 commit/effect path。

## Risks / Trade-offs
- Risk: Effect 层绕过规则裁决直接实现移动或推动。
  Mitigation: spec 明确禁止 Effect 直接提交坐标，移动类结果只能改变 final component/tag/stat，后续 action 再走 claim/arbitration。
- Risk: CommitResolver 变成玩法解释器。
  Mitigation: Commit 只识别通用写入种类和 deterministic order，不根据 action/effect readable name 分支。
- Risk: effect remove 误删静态来源或其他 buff 来源。
  Mitigation: 每个 effect application 拥有独立 source key；remove 只移除匹配 source，final result 由 resolver/settlement 从剩余 source 重算。
- Risk: EffectSpec 大表膨胀。
  Mitigation: 第一片 Luban 独立表只保留 typed payload、duration、stack、target binding、cue id；复杂 target 和 activation 留在已有 Targeting/Action/未来 Ability 层。
- Risk: Debug runtime effect 继续走旁路。
  Mitigation: apply 阶段必须迁移 debug handler 或新增测试证明 debug 也生成 effect/commit。

## Migration Plan
- 保留现有 `RuntimeEffectSpec` 作为第一片 runtime payload 或兼容 adapter。
- 新增 Luban `effect_spec` 后，现有 temporary blocking/auto move/pushable/port/immobile 映射为 effect payload。
- 迁移完成前允许 debug path 使用 adapter，但测试必须覆盖 adapter 不直接写 final component。
- 迁移完成后，新增普通 effect 不允许绕过 `EffectApplication -> CommitProposal -> RuntimeEffectStore -> ComponentStateResolver`。

