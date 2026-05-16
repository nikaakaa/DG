# Change: 扩充 Effect Application 层

## Why
当前 `RuntimeEffectStore`、`RuntimeEffectSpec` 和 `ComponentStateResolver` 已经能表达运行时效果来源到 final Component 的合成边界，但 action 管线还没有稳定的 `EffectSpec -> EffectApplication -> Commit -> RuntimeEffectStore -> ComponentStateResolver` 闭环。继续让调试工具、strategy 或临时逻辑直接构造 runtime effect，会让“技能发动”和“效果状态”边界混在一起。

本变更把 Effect 层作为小 GAS 的下一片落地：Action 负责命中、上下文和执行入口，Effect 负责对目标施加持续状态、组件结果、tag 结果和生命周期，Commit 负责唯一写入权威世界状态。

## What Changes
- 新增 Luban 独立 `EffectSpec` 表作为静态效果定义来源，描述 effect kind、target binding、duration、stack policy、remove policy、component/tag result 负载和 presentation metadata id。
- 新增 `EffectApplication` 作为运行时效果实例输入，携带 action context、target data、source、target、start tick、expire tick、stack key、causality 和 resolved payload。
- tag result 第一片继续使用现有 `WorldTag`，不引入新的 tag registry。
- 扩展 action execution 输出，使 `ApplyRuntimeEffect` 或 future `ApplyEffectsToTargets` 能从命中的 `TargetData[]` 生成 `EffectApplication[]`，而不是直接写 `GameWorld`。
- 扩展 `CommitProposal` 语言，支持 `AddRuntimeEffect`、`RemoveRuntimeEffect`、`SetComponentResult`、`AddTag`、`RemoveTag` 的第一片边界，并保持 deterministic commit。
- 让 `RuntimeEffectStore` 存储由 commit 接受后的 effect application/runtime effect source，继续不直接写 `GameWorld` component store。
- Effect 对 component/tag 的影响按独立 source/contribution 保留；最终 component/tag 结果通过 resolver/settlement 统一结算，remove effect 只移除自己的 source。
- 让 `ComponentStateResolver` 消费 accepted runtime effect sources，合成 final Component/tag 结果；Rules/Arbitration 仍只读取 final result，不查询 effect store 或 effect kind。
- 保留现有 debug apply/remove runtime effect 能力，但把它迁移为显式 commit/effect path 的调试来源。
- 增加 Unity TestFramework EditMode、Shared build、server verification 和手动 Play Mode 双客户端验证要求。

## Non-Goals
- 不在 proposal 阶段写实现代码。
- 不引入完整 `AbilitySpec`、Ability 激活模型、冷却、蓄力、引导、取消或输入缓冲。
- 不引入通用 Attribute/Stat 系统；本变更只保留 future stat payload 边界。
- 不引入层级 GameplayTag registry；本变更只允许通过现有 tag/final result 边界扩展。
- 不实现复杂 target query；TargetData 来源依赖当前或已批准的 targeting 管线。
- 不让 Effect 直接裁决移动、推动、冲突、坐标提交或命中规则。
- 不让 Unity client 本地判断 effect 是否命中、是否生效或是否过期。

## Impact
- Affected specs:
  - `runtime-component-results`
  - `data-driven-runtime-actions`
  - `client-world-runner`
- Related active changes:
  - `refactor-authoritative-action-pipeline-foundation`
  - `refactor-action-policy-pipeline`
  - `add-action-targeting-system`
- Affected code during apply:
  - `Shared/DG.GameCore/RuntimeEffects/*`
  - `Shared/DG.GameCore/Rules/Actions/*`
  - `Shared/DG.GameCore/Rules/Commit/*`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/Config/LubanActionSpecRegistry.cs`
  - `Config/Luban/Defines/gamecore.xml`
  - Luban `effect_spec` 数据表与生成数据
  - `Config/Luban/Datas/gamecore/*`
  - Server debug runtime effect handlers
  - Unity TestFramework EditMode tests
  - Server authoritative verification

## Verification
- Proposal validation:
  - `openspec validate add-effect-application-layer --strict --no-interactive`
- Apply-stage automated validation:
  - `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
  - `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
  - Unity TestFramework EditMode tests for EffectSpec import, EffectApplication creation, runtime effect commit, removal policy, stack policy, resolver final component/tag output, and rule-layer no-effect-store-query guard
- 用户手动端到端验证：
  - Play Mode 连接服务端权威路径
  - 双客户端 observer 收到一致 `WorldDelta`
  - 使用调试工具或配置行为施加 temporary pushable、temporary immobile、temporary auto move 或 temporary tag，确认服务端生效、过期/移除后同步一致
