## Context
Shared GameCore 当前承担的是服务端权威行为运行时，不只是“规则”或“移动”。现有目录把 action 规格、策略注册、targeting、blocked outcome、claim、planning、commit、component fact query、generated registration 混在 `Rules/Actions` 附近，导致两个问题：

1. 新行为接入时看不出应该新增配置、生成注册、策略模块，还是修改中心编排。
2. `ActionBlockedOutcomeExecutor` 这类类名像底层求解器，但内部包含 bounce、derive、reject 等业务 outcome 分支，文件边界会诱导继续堆逻辑。

## Final Shared Layout
目标目录结构如下，先对 Shared 层收口：

```text
Shared/DG.GameCore/
  ActionRuntime/
    Specs/
    Requests/
    Queue/
    Lifecycle/
    Execution/
    Strategies/
      Contracts/
      Implementations/
    Targeting/
      Contracts/
      Selectors/
      Filters/
    Gating/
      Conditions/
    Subjects/
    Claims/
    Blocking/
      Contacts/
      Policies/
      Outcomes/
    Planning/
    Commit/
      Contracts/
      Handlers/
    Generated/
  Configuration/
    Luban/
    Generated/
  Domain/
    Components/
    Entities/
    Ids/
    ValueObjects/
  World/
    Storage/
    Spatial/
    Snapshots/
    Delta/
  RuntimeEffects/
    Specs/
    Store/
    Settlement/
  Testing/
```

## Boundary Rules
- `ActionRuntime` 是行为 pipeline 的唯一源码入口，替代 `Rules` 作为扩展入口。
- `Execution` 只负责编排：按 spec 查 policy，调用 gate、subject、targeting、strategy、claim、planning、commit，不放普通行为分支。
- `Strategies/Implementations` 放可复用动作能力，例如 move、spawn、remove、component result、effect result 的执行输出构造。
- `Blocking/Contacts` 只收集阻挡事实；`Blocking/Policies` 只匹配 policy；`Blocking/Outcomes` 放 derive、bounce、reject、noop 等业务 outcome 实现。
- `Commit/Handlers` 放具体 world mutation handler；`Commit` 根目录只保留 proposal/result/context/registry 等编排类型。
- `Generated` 只放自动生成注册或 Luban 生成数据，不放人工业务逻辑。
- `Domain` 放 component、entity id、稳定值对象；不依赖 action runtime。
- `World` 放存储、空间索引、snapshot/delta 容器；不选择普通行为。
- `Configuration/Luban` 放 Luban 到 runtime spec 的 provider/importer；`Configuration/Generated` 放 Luban 生成表。

## Migration Strategy
迁移按源码边界而不是行为功能切：

1. 先建立目录并移动纯模型和 registry 文件，保持命名空间不变。
2. 再拆过大的文件，例如 `ActionStrategyRegistry.cs`、`ActionTargeting.cs`、`CommitRules.cs`。
3. 最后处理 `ActionBlockedOutcomeExecutor`：保留一个 executor/orchestrator，但把具体 outcome 放入 `Blocking/Outcomes`。
4. 更新生成器输出路径到 `ActionRuntime/Generated`。
5. 每一步都跑 Shared build、server verification、Unity EditMode tests。

## Relationship To Active Changes
`refactor-action-component-extension-boundaries` 定义“新增扩展点不改中心类”的能力边界；本变更定义这些边界在 Shared 源码里应该住在哪里。若两个变更同时 apply，实现顺序应先完成扩展边界，再执行源码路径迁移，避免一边拆文件一边改语义。

`refactor-server-world-storage-to-arch-backend` 属于 world storage backend，不改变本提案的 `World/Storage` 目标目录。

## Validation
- `openspec validate refactor-shared-gamecore-source-layout --strict --no-interactive`
- `dotnet build Shared/DG.GameCore/DG.GameCore.csproj`
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj`
- Unity TestFramework EditMode：覆盖 action strategy generated registration、target selector registry、blocked outcome policy dispatch、commit handler registry、component fact query、snapshot/component result projection。
- 用户手动端到端：Play Mode 或两客户端验证 player move、auto move、push/bounce、debug spawn/remove、snapshot/delta。
