# Change: 将 ActionClaim 接入行为仲裁主流程

## Why
`refactor-data-driven-runtime-actions` 已经让运行时入口引用 `ActionSpec`，但源码中 `StateDrivenRules` 仍在处理 target 解析、body 投影、blocker、pushable、bounce 和 pending push 推进；`ActionArbiter.TryBuildMoveClaims` 生成的 claim 也还没有进入主仲裁流程。

这会导致“行为配置统一了，但仲裁语义没有统一”：例如 port-connected body 的任意 member 撞到 pushable 时，需要 system 层临时补 body blocker 检查，新增普通行为仍可能继续把规则分散进 execution / planner。

## What Changes
- 引入 claim-driven action arbitration 作为运行时主仲裁模型。
- 让 `ActionArbiter` 从 `ActionRequest + ActionSpec` 解析 target、body、claims、blocker、derived pending request 和 action result。
- 让冲突、合并、打断由 claim group、priority、merge policy、interrupt policy 和 final component/tag 结果决定。
- 让 `StateDrivenRules` 只负责 orchestration：adapter、arbiter、planner、commit、result application。
- 让 `RulePlanner` 消费仲裁接受的 action / claims，减少重复 occupancy 判断。
- 保留旧 `BehaviorIntent` 作为短期兼容桥时，必须证明核心仲裁决策已经由 `ActionArbiter` 输出。

## Impact
- Affected specs: `claim-driven-action-arbitration`
- Related active change: `refactor-data-driven-runtime-actions`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Arbitration/IntentArbiter.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/*`
  - `Server/Tests/AuthoritativeMoveVerification/*`

## Out Of Scope
- 不新增完整 Ability / GAS。
- 不新增 Unity 客户端本地权威规则。
- 不迁移到 Unity Player build 验证。
- 不在本提案中要求正式 Luban Excel 接入 action spec，仍可先使用 Shared registry / fallback 数据。
