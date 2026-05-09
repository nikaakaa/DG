# Change: 重构 RuntimeEffect 到 Component 结果和规则仲裁边界

## Why
当前工程基线没有 `RuntimeEffectStore`、`EffectRules` 或 `ComponentStateResolver` 实现，旧 runtime ability / runtime effect 方向已被撤回。继续用 Effect、Debug UI 或 Ability 直接增删 `GameWorld` component 会再次混淆状态来源、最终结果和规则仲裁，尤其会出现 runtime source 移除时误删 static component 的问题。

本变更先建立最小状态模型：静态数据和运行时 effect 数据都是 Component 之前的输入，`ComponentStateResolver` 合成最终 Component 结果，System / Rules 只读取最终 Component，并由规则层处理动态 port、可移动 / 不可移动、推动和冲突仲裁。

## What Changes
- 新增 `runtime-component-results` 能力规格，定义静态数据层、运行时 effect 数据层、状态解析层、Component 结果层和 Rules 仲裁边界。
- 引入 `ComponentStateResolver` 作为 `static + runtime -> final Component` 的唯一合成边界。
- 第一阶段覆盖存在性 / 能力型 Component：`BlockingComponent`、`AutoMoveComponent`、`PushableComponent`、`PortConnectorComponent` 和移动权限类结果。
- 第一阶段明确排除 `PositionComponent`、`DirectionComponent`、`PlayerControlComponent` 和 runtime tick counter。
- 规定 `RuntimeEffectStore` 只保存运行时 effect 状态，不能直接写 `GameWorld` component store。
- 规定本阶段不引入 Ability，不用 Ability 解决 Component 生命周期、动态 port 或移动仲裁。
- 规定动态 port 通过 runtime/static sources 合成 final `PortConnectorComponent`，由 `PortConnectionSystem` 读取最终结果。
- 规定可移动 / 不可移动通过 final Component result + Rules 仲裁，不由 effect store 或 Ability 判断。
- 规定 `ClientMapWorld` 只镜像服务端最终 Component 结果，不在客户端本地解析 runtime effect / ability。
- 保留 `WorldTag` 为短期迁移遗留语言，不在第一阶段继续扩大新的 System 依赖。

## Non-Goals
- 不实现完整 GAS、cooldown、cost、combo、channeling、attribute、GameplayCue、预测或回滚。
- 不恢复 stash 中的旧 runtime effect / ability / debug UI 半成品。
- 不实现 Ability 系统。
- 不让 Ability 直接映射到 Component。
- 不让 Ability 管理 Effect 或 Component 生命周期。
- 不用 Ability 解决动态 port 或可移动 / 不可移动仲裁。
- 不让 Effect 直接写 `GameWorld.SetComponent` 或 `GameWorld.RemoveComponent`。
- 不把所有 Component 都纳入 resolver，尤其不处理坐标、方向和规则 commit 值结果。
- 不让 Unity UI 或 `ClientMapWorld` 执行服务端权威规则裁决。

## Grounding
- `openspec list` 当前结果为 no active changes。
- 现有 specs 包含 `shared-gamecore-entity-rules`、`client-world-runner`、`authoritative-move-runner`、`map-runtime-foundation`、`multiplayer-entity-management`。
- 当前 `EntityBuilder` 通过 `ComponentApplicationRegistry` 把 archetype component kind 直接应用到 `GameWorld`。
- 当前 `GameWorld` 保存最终 component store、空间索引、dirty、snapshot/delta。
- 当前 `StateDrivenRuleExecutionSystem` 读取 `AutoMoveComponent`、`BlockingComponent`、`PushableComponent` 等最终 component 做规则输入。
- 当前 `PortConnectionSystem` 应读取最终 `PortConnectorComponent`，动态 port 不能绕过 resolver 直接写 world。
- 当前 `ClientMapWorld` 包装 Shared `GameWorld`，通过 snapshot/delta 应用服务端状态。
- `docs/Goal/goal.md` 是本轮提案的唯一可读目标文档；IDE 中打开的旧 subgoal 文档在当前工作树中已删除，未作为提案来源。

## Impact
- Affected specs:
  - `runtime-component-results`
  - `client-world-runner`
- Affected code:
  - `Shared/DG.GameCore/Components`
  - `Shared/DG.GameCore/Config/EntityBuilder.cs`
  - `Shared/DG.GameCore/Config/ComponentApplicationRegistry.cs`
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/Snapshots/Snapshot.cs`
  - `Shared/DG.GameCore/Rules`
  - `Tools/NetworkProtocol/Outer/OuterMessage.proto`
  - `Client/DG_Client/Assets/Scripts/ClientWorld`
  - Unity TestFramework EditMode tests

## Validation
- OpenSpec: `openspec validate refactor-static-runtime-component-results --strict --no-interactive`
- Unity TestFramework EditMode:
  - static + runtime 同 Component 不误删
  - runtime 多来源引用
  - RuntimeEffectStore 不直接写 Component
  - 不引入 Ability / 不出现 `AbilityKind -> ComponentKind`
  - dynamic port 合成与移除
  - movement permission 仲裁
  - Rules 不查询 Ability / Effect
  - ClientMapWorld 只镜像服务端最终结果
- 手动端到端:
  - 服务端运行后打开两个 Unity 客户端
  - A 通过调试入口施加临时 AutoMove / Blocking / Pushable 类 runtime effect
  - B 只通过服务端 WorldDelta 看到最终 Component 结果
  - 移除 runtime effect 后，静态 component 仍保留，纯 runtime component 才消失
  - 不执行 Unity Player build
