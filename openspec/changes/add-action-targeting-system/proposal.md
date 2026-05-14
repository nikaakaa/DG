# Change: 增加权威 Action Targeting 系统

## Why

`docs/Goal/authoritative-action-effect-system.md` 已经把 Targeting / Query 列为权威 Action / Effect 管线缺失的关键层。当前代码虽然已有 `ActionContext`、`ActionTargetData`、`ActionExecutionOutput`，并且 `ActionTargetSelector` 已能处理 `Self`、`DirectionCell`、`FrontEntities` 等规则，但目标选择仍主要由 `ActionSpec.TargetRule` 枚举和一个集中 selector 承担。长期形态应该是每一种目标选择算法都有独立的 selector 类或扩展类，再由配置字段映射到对应 selector，而不是继续把所有算法塞进一个枚举分支或一个大方法里。

如果继续在 `ActionTargetSelector` 里追加规则，新的范围目标、组件/tag 过滤、多目标命中、all-or-nothing fanout 和未来 Effect 目标绑定都会再次挤进移动/推动管线。这个变更把 targeting 作为独立系统落地，让 action 管线变成：

```text
ActionContext -> TargetingSystem -> TargetData[] -> ExecutionOutput -> Claim -> Arbitration -> Planning -> Commit
```

## What Changes

- 新增权威 `TargetingSystem` 概念，负责从 `ActionContext + ActionSpec/TargetingSpec + GameWorld` 生成只读 `TargetData[]`。
- 新增目标选择算法类/扩展类注册边界，配置字段只选择 selector 和参数，不直接承载全部算法实现。
- 新增 `TargetingSpec` / `TargetFilter` 配置边界，逐步替代只靠 `ActionTargetRule` 枚举表达普通目标选择。
- 第一阶段只实现底层 targeting 抽象、注册、配置映射和现有基础目标选择的迁移；`FrontLine`、`FrontEntities`、box、circle 等作为后续 selector 类扩展，不纳入本切片。
- 明确 `TargetData` 是候选目标集合，不是 claim、plan 或 commit；Targeting 不写 `GameWorld`，不派生 action，不裁决 blocked/push/bounce。
- 多目标输出必须有稳定排序和命中元数据，execution/claim fanout 必须消费同一组 `TargetData[]`。
- Luban 配置导入需要把可读 targeting/filter 名称解析成运行时 typed/numeric id 或 enum，规则层不比较原始字符串。
- Unity 客户端只消费服务端最终 snapshot/delta，不在本地复刻 targeting 裁决。

## Non-Goals

- 不在 proposal 阶段写实现代码。
- 不一次性实现完整查询语言、脚本 VM、行为树节点或 UE GAS TargetActor。
- 不在本变更实现 `FrontLine`、`FrontEntities`、box、circle 等具体范围算法；这些作为后续 selector 类扩展。
- 不改变服务端权威 `GameWorld` 是唯一规则真相源的边界。
- 不让 Targeting 直接处理冲突、阻挡、推动、反弹、计划或提交。
- 不引入 UnityEngine、Fantasy、DOTS 或第三方 ECS 依赖到 `Shared/DG.GameCore`。

## Impact

- Affected specs:
  - `data-driven-runtime-actions`
  - `claim-driven-action-arbitration`
  - `shared-gamecore-entity-rules`
- Related active changes:
  - `refactor-authoritative-action-pipeline-foundation`
  - `refactor-action-policy-pipeline`
  - `refactor-auto-move-self-push`
- Affected code during apply:
  - `Shared/DG.GameCore/Rules/Actions/ActionPolicyModels.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionPipeline.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecRegistry.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Config/LubanActionSpecRegistry.cs`
  - `Config/Luban/Defines/gamecore.xml`
  - Luban action/targeting/filter Excel source and generated JSON
  - Unity TestFramework EditMode tests
  - Server authoritative verification tests

## Verification

- Proposal validation:
  - `openspec validate add-action-targeting-system --strict --no-interactive`
- Apply-stage automated validation:
  - `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
  - `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
  - Unity TestFramework EditMode tests for targeting spec import, selector registry mapping, self target, direction cell target, target coord target, component/tag filtering, no-world-write guard, and target fanout into claims
- 用户手动端到端验证:
  - Play Mode 连接服务端权威路径
  - 双客户端 observer 收到一致 `WorldDelta`
  - 至少验证玩家单格移动、AutoMove self target 和机关推动都由服务端 targeting 裁决后同步
