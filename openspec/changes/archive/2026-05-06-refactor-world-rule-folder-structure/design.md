## Context
当前规则链路已经收口到 `WorldActionQueue -> StateDrivenRuleExecutionSystem -> BehaviorIntent -> IntentArbiter -> RulePlanner -> ConflictResolver -> CommitProposalResult`。问题不再是运行时链路混乱，而是目录仍保留早期 `Movement`、`Map` 命名，和当前职责不匹配。

## Goals / Non-Goals
- Goals: 让 Shared 目录表达规则层职责，让客户端目录表达 world mirror/runtime/network/view 职责。
- Goals: 保持服务端权威规则行为、snapshot/delta 行为和现有测试语义不变。
- Goals: 删除 legacy 概念入口，避免后续开发误回旧裁决路径。
- Non-Goals: 不引入客户端预测、rollback、AOI、技能系统或新配置表。
- Non-Goals: 不把 component-system 扩展成完整 GAS。
- Non-Goals: 不改 Unity Player build 流程。

## Decisions
- Decision: `Shared/DG.GameCore/Movement` 重命名为 `Shared/DG.GameCore/Rules`。
- Reason: 当前目录内容代表世界规则执行管线，不再只是移动。
- Alternative: 使用 `Simulation`。
- Rejected: `Simulation` 过宽，容易把 world storage、snapshot、config 也吸进去。

- Decision: 客户端 `Assets/Scripts/Map` 重命名为 `Assets/Scripts/ClientWorld`。
- Reason: 当前模块主要是客户端 world mirror、网络同步、显示和调试工具，不是纯地图结构。
- Alternative: 使用 `WorldClient`。
- Rejected: `ClientWorld` 更贴近现有 `ClientMapWorld` 和 `ClientWorldRunner` 的命名方向。

- Decision: 第一阶段目录迁移时优先保持运行时行为不变。
- Reason: 这次变更目标是清理物理边界，不同时改变规则语义，便于验证。

- Decision: namespace 是否同步重命名按风险分层执行。
- Reason: Unity 脚本、测试和场景序列化对类型名敏感。实现阶段必须先审计 `MonoBehaviour` 类型和场景引用，再决定是否从 `DG.Map` 同步迁移到 `DG.ClientWorld`。如果会造成大量 Unity 序列化风险，第一阶段只移动目录，后续单独做 namespace 迁移。

## Risks / Trade-offs
- Risk: Unity `.meta` 丢失会导致场景或 prefab 脚本引用断开。
- Mitigation: 使用移动文件而不是删除重建，迁移后刷新 Unity 并运行 EditMode 测试。

- Risk: 一次性 namespace 重命名会影响 MonoBehaviour 序列化和 generated csproj。
- Mitigation: 先做引用审计；必要时目录先行、namespace 后续。

- Risk: Shared 文件迁移后 Unity 生成的 `DG.GameCore.csproj` 缓存旧路径。
- Mitigation: 迁移后刷新 Unity scripts/assets，再跑 Shared build 和 EditMode。

## Migration Plan
1. 迁移 Shared `Movement` 下的纯 C# 文件到 `Rules`。
2. 将 `BehaviorArbitration.cs` 拆成 intent、arbitration、planning、commit 相关文件。
3. 删除空 legacy 目录和残留 `.meta`。
4. 迁移 Unity `Map` 目录到 `ClientWorld`。
5. 审计并更新引用、asmdef/csproj/测试路径。
6. 运行 OpenSpec、Shared build、服务端验证和 Unity EditMode。

## Open Questions
- 实现阶段是否同步把 `DG.Map` namespace 改为 `DG.ClientWorld`，取决于 Unity 场景和 prefab 引用审计结果。
