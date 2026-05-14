# Change: 建立权威 Action 管线底座

## Why

当前行为层已经有 `ActionSpec`、strategy registry、claim arbitration、deferred output 和 commit 边界，但核心数据流仍然偏向“一个 entity + 一个 direction/target coord 的 move/push”。`ActionRequest.EntityId`、`ActionTarget`、`ActionTargetSelector`、`ActionArbiter` 和 blocked outcome 之间缺少显式 `ActionContext`、`TargetData`、`ExecutionOutput` 分层，导致多目标、self push、面前所有对象移动、部分成功策略和结果归属都容易继续挤进中央仲裁器或特殊分支。

本变更把权威行为骨架明确为：

```text
ActionContext -> TargetData -> ExecutionOutput -> Claim -> Arbitration -> Planning -> Commit
```

它是 `refactor-action-policy-pipeline` 的更具体数据流底座，也是 `refactor-auto-move-self-push` 需要依赖的上下文/目标/输出边界。目标不是恢复旧 pending parent/child，也不是一次性实现完整 GAS，而是让现有 move/push 管线具备可扩展的目标选择、执行输出和多 claim 闭环。

## What Changes

- **BREAKING**: action runtime model 必须区分 instigator/source/causer、subject entry、target hint、resolved subject、target data、execution output 和 result owner，不能继续让 `EntityId` 同时承担所有含义。
- 建立 `ActionContext` 作为每次权威 action 的运行时上下文入口，承载来源、归属、ready/cost tick、causality、subject entry 和 target hint。
- 建立 `TargetData` 作为 targeting/query 的只读输出，先支持 Self、DirectionCell 和 FrontCells/FrontEntities 这类最小能力，后续区域/组件/tag 查询沿同一模型扩展。
- 建立 `ExecutionOutput` 作为 strategy/policy 产物，负责把 context + target data 转成候选 claims、blocked outcome、deferred output 或 commit output。
- 多个 `TargetData` 必须能产生多个 claims；“面前所有对象移动”的最小权威闭环必须通过 TargetData fanout -> ExecutionOutput -> Claim -> arbitration -> planning -> commit 表达。
- all-or-nothing 和 partial success 不要求本阶段完整实现所有玩法策略，但必须有明确数据入口和 policy 字段边界，不能靠中央类硬编码。
- AutoMove self push 必须通过 context 表达 source 与 target 都是自身，并通过 execution output / blocked policy 归属结果，不依赖旧 pending parent/child。
- 保持服务端 `GameWorld` 为唯一权威裁决源，Unity client 只镜像最终结果。

## Non-Goals

- 不在 proposal 阶段写实现代码。
- 不实现完整 UE GAS、脚本 VM、行为树或通用表达式系统。
- 不恢复 `PendingRuleStates`、parent waits child、parent retry 成功链作为普通 push 默认路径。
- 不把全链 push 做成同 tick 无限求解。
- 不把客户端表现层变成权威 action/target/effect 裁决层。
- 不一次性完成所有区域 targeting、伤害、状态、runtime effect 应用和复杂 partial success 策略。

## Impact

- Affected specs:
  - `data-driven-runtime-actions`
  - `claim-driven-action-arbitration`
  - `atomic-action-behavior-layer`
  - `shared-gamecore-entity-rules`
- Related active changes:
  - `refactor-action-policy-pipeline`
  - `refactor-auto-move-self-push`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionRequests.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionPipeline.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionArbiter.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionPolicyModels.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Commit/*`
  - `Shared/DG.GameCore/Config/LubanActionSpecRegistry.cs`
  - Luban action / targeting / execution policy data
  - Unity TestFramework EditMode tests
  - Server authoritative verification

## Verification

- `openspec validate refactor-authoritative-action-pipeline-foundation --strict --no-interactive`
- Apply stage automated validation:
  - `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
  - `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
  - Unity TestFramework EditMode tests for ActionContext, TargetData, ExecutionOutput, multi target claim fanout, self push context, all-or-nothing and partial policy entry guards
- 用户手动端到端验证：
  - Play Mode 连接服务端权威路径
  - 双客户端 observer 收到一致 `WorldDelta`
  - AutoMove self push、普通方向移动、面前所有对象移动的最小场景都由服务端裁决并同步
