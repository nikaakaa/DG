## Context
当前代码已经有 `ActionSpec`、`ActionRequest`、`ActionClaim` 和 `ActionArbiter`，但主流程仍是：

`WorldAction -> ActionRequest -> StateDrivenRules.ProcessMove -> BehaviorIntent -> IntentArbiter -> RulePlanner -> ConflictResolver -> Commit`

源码缺口集中在：
- `StateDrivenRules.ProcessMove` 仍处理 target、blocker、pushable、bounce。
- `StateDrivenRules.FindBlockingForBodyMove` 临时处理 port-connected body blocker。
- `StateDrivenRules.AdvancePushStates` 手写 pending push 链推进。
- `ActionArbiter.TryBuildMoveClaims` 没有被 runtime 主流程消费。
- `IntentArbiter` 仍按 `BehaviorIntent` 做 tag、movement permission、priority、merge 和 conflict。
- `RulePlanner` 仍重新解析 body 并重新检查 occupancy。

## Goals
- 让 `ActionClaim` 成为 move 类行为的主仲裁数据。
- 让 port-connected body 的任意 member 与玩家本体使用同一套 blocker / pushable 语义。
- 让 pending push continuation 也走 `ActionRequest + ActionSpec + ActionClaim`。
- 让行为优先级、source、blocked policy、merge policy、interrupt policy、commit policy 都由 `ActionSpec` 或配置 registry 提供。
- 让新增普通行为只改配置/registry/tests，不改 core execution / arbitration orchestration。
- 保持现有玩家移动、自动移动、机关推动、调试编辑和 WorldDelta 结果兼容。

## Non-Goals
- 不实现完整技能系统。
- 不让 client 裁决服务端权威规则。
- 不删除所有旧类型作为第一步目标；`BehaviorIntent` 可短期作为 planner adapter，但不能继续承载核心仲裁决策。
- 不做 Unity Player build。

## Decisions
- Decision: 新增 `ActionArbitrationResult` 聚合 accepted actions、rejected actions、derived pending actions、commit proposals、action results 和 reasons。
- Decision: 新增 `AcceptedAction` 表达仲裁通过后的 request/spec/body/direction/target/claims。
- Decision: `ActionArbiter` 负责 move target 解析、body 投影、final component/tag 检查、blocker 检查、claim 生成、same-claim merge、priority interrupt 和 blocked policy。
- Decision: `ActionSpec` 是行为静态策略来源，priority、source、required tags、blocked tags、blocked policy、conflict policy、merge policy、interrupt policy、plan rule 和 commit rule 不得在 `StateDrivenRules` 中硬编码。
- Decision: `StateDrivenRules` 调用 `ActionArbiter` 后只把 accepted actions 交给 planner，把 proposals 交给 commit。
- Decision: `RulePlanner` 增加从 `AcceptedAction` 或 accepted claims 生成 `MovePlan` 的路径，旧 `BehaviorIntent` 路径仅用于迁移期测试对照。
- Decision: pending push 的下一步以 `ActionRequest` 形式重新进入 `ActionArbiter`，不在 system 层手写特殊推进分支。
- Decision: 本阶段保留 `BehaviorIntent` 作为 planner adapter 或测试对照，不让它继续承担主仲裁决策；等 accepted action / claims 路径稳定后再单独清理。
- Decision: 本阶段先用 `BodyMove.ToCoord` 覆盖目标格独占，只有当非移动行为也需要抢占格子或资源时，再把 `ActionClaimKind.TargetCell` 做成独立必经 claim。
- Decision: 本阶段 `debug_move` 继续作为 `Move + TargetCoordAny + Debug source` 兼容存在；`Teleport` primitive 后续单独提案，避免本阶段扩大原语范围。

## Migration Plan
1. 保留现有测试，新增 claim-driven arbitration 测试作为行为锁。
2. 新增 arbitration result model，不先删除旧 `BehaviorIntent`。
3. 将 `FindBlockingForBodyMove` 和 target/body projection 迁入 `ActionArbiter`。
4. 将 `ProcessPushableBlock`、`ProcessBounceBlock` 和 tag/movement permission 检查迁入 `ActionArbiter`。
5. 将 same-claim merge、conflicting body claim 和 priority interrupt 迁入 `ActionArbiter`。
6. 让 `RulePlanner` 支持 accepted claims。
7. 简化 `StateDrivenRules`。
8. 移除或降级旧 `IntentArbiter` 在主流程中的决策职责。

## Risks / Trade-offs
- Risk: 一次性替换主流程可能破坏现有移动/推动结果。
  - Mitigation: 先并行新增 accepted action 路径和等价测试，再切主流程。
- Risk: claim 模型过度抽象。
  - Mitigation: 第一阶段只覆盖 move/body/target/blocker/pending push，不扩展到未使用的技能效果。
- Risk: pending push 链和 port-connected body 组合容易漏边界。
  - Mitigation: Unity EditMode 加覆盖：root 撞 pushable、linked port 撞 pushable、port body 外部 blocker、同 body same claim、同 body conflicting claim。

## Resolved Boundaries
- `BehaviorIntent`：本阶段保留为迁移桥，但主仲裁结果以 `ActionArbitrationResult / AcceptedAction / ActionClaim` 为准。
- `ActionClaimKind.TargetCell`：本阶段不强制独立落地，先由 `BodyMove.ToCoord` 承担 move 目标格独占。
- debug teleport：本阶段继续复用 `debug_move` 的 `Move + TargetCoordAny`，后续再决定是否拆出 `Teleport` primitive。
