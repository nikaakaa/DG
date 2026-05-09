## Context
DG 当前已经有服务端权威的 Shared GameCore、state-driven action / intent / plan / commit 管线，以及 Unity 客户端镜像服务端 snapshot/delta 的基础。实体出生时的静态 component 由 `EntityBuilder` 和 `ComponentApplicationRegistry` 从 Luban archetype 应用到 `GameWorld`。

Runtime Effect 的下一步不能沿用旧半成品思路。旧思路的问题是把 Ability、Effect、Debug UI 都变成 Component 管理器，导致多个来源随手写 `GameWorld`，System 也容易同时理解 Component、Tag、Ability、Effect。

本设计把问题收窄到第一阶段：建立静态数据和运行时 effect 数据到最终 Component 结果的边界，并让动态 port、可移动 / 不可移动进入 final Component result + Rules arbitration，而不是引入 Ability 作为中间概念。

## Goals / Non-Goals
- Goals:
  - 定义 `static source + runtime source -> Component result` 的统一模型。
  - 让 System / Rules 只读取最终 Component 结果。
  - 让 `RuntimeEffectStore` 只保存 runtime effect 生命周期状态。
  - 让 `ComponentStateResolver` 负责合成和应用最终结果。
  - 让动态 port 通过 final `PortConnectorComponent` 被 `PortConnectionSystem` 读取。
  - 让可移动 / 不可移动通过 final Component result 被 Rules 仲裁。
  - 用 Unity TestFramework EditMode 覆盖 resolver 行为和客户端镜像边界。
- Non-Goals:
  - 不引入 Ability 系统。
  - 不做完整 Debug UI。
  - 不做客户端预测、回滚或插值。
  - 不把 `PositionComponent` / `DirectionComponent` 这类 commit 值结果纳入 resolver。
  - 不从 stash 恢复旧实现。

## Layer Model
```text
Luban EntityArchetype / ComponentKind / EffectDefinition
        |
        v
静态数据层

RuntimeEffectSpec / RuntimeEffectInstance / debug source / trigger source
        |
        v
运行时数据层

静态数据 + 运行时数据
        |
        v
ComponentStateResolver
        |
        v
Component 结果层
        |
        v
System / Rules
        |
        v
WorldAction / Intent / Plan / Commit
        |
        v
GameWorld final state / Dirty / Delta
        |
        v
ClientMapWorld mirror
```

## Decisions
- Decision: 新增独立 `runtime-component-results` 能力规格。
  - Rationale: 现有 `shared-gamecore-entity-rules` 已描述组件驱动规则和 state-driven 主路，但没有描述 runtime source 与 final component result 的来源隔离。单独能力更容易审查边界。

- Decision: 第一阶段解析 `BlockingComponent`、`AutoMoveComponent`、`PushableComponent`、`PortConnectorComponent` 和移动权限类结果。
  - Rationale: 它们是存在性 / 能力型结果，适合证明 static 与 runtime 来源合成。动态 port 和可移动 / 不可移动是当前真实复杂行为问题，必须通过 final result + Rules 解决。`PositionComponent` 和 `DirectionComponent` 是规则 commit 值结果，放进 resolver 会混淆规则裁决与状态来源解析。

- Decision: `RuntimeEffectStore` 不引用 `GameWorld`，也不暴露 `SetComponent` / `RemoveComponent` 类 API。
  - Rationale: Effect 是运行时来源数据，不是最终世界状态。最终状态必须由 resolver 合成后应用。

- Decision: `ComponentStateResolver` 维护 source-aware 合成关系。
  - Rationale: 同一个 final component 可能同时来自 static archetype 和多个 runtime effect。移除任意 runtime source 时，只有当没有其他 static/runtime source 时才移除 final component。

- Decision: System / Rules 不注入或查询 `RuntimeEffectStore`、`AbilityKind`、`EffectKind`。
  - Rationale: System / Rules 的输入语言保持为 final Component，这能避免规则层再次分裂成多套状态判断。

- Decision: 本阶段不引入 Ability。
  - Rationale: Ability 不能解决动态 port，也不能解决可移动 / 不可移动仲裁。如果现在只作为 `WorldAction` 或 `RuntimeEffectSpec` 的包装，它不会增加能力，只会引入没有明确职责的抽象。

- Decision: 动态 port 使用 source-aware merge。
  - Rationale: static port 与 runtime port 可能同时存在，runtime port 移除后 static port 必须保留。第一阶段默认采用 union merge，后续需要 override 或优先级时单独扩展。

- Decision: 可移动 / 不可移动由 final Component result + Rules arbitration 决定。
  - Rationale: Effect 生命周期只说明状态是否存在；是否能移动、能否被推动、是否被打断，需要由 `IntentArbiter`、`RulePlanner`、`ConflictResolver` 在读取最终结果后裁决。

- Decision: `WorldTag` 第一阶段作为迁移遗留保留。
  - Rationale: 当前 `BehaviorIntentDefinitions` 和 arbitration 已依赖 `WorldTag` 表达 source、ability、blocked state。第一阶段不扩大新 tag 依赖，也不强行一次性迁移到 marker component。

- Decision: `ClientMapWorld` 只应用服务端下发的 final component result。
  - Rationale: 客户端不应该根据 runtime effect 或 ability 在本地重新计算权威结果；它只镜像服务端 snapshot/delta，并驱动 Unity view。

## First Slice Components
- Included:
  - `BlockingComponent`
  - `AutoMoveComponent`
  - `PushableComponent`
  - `PortConnectorComponent`
  - 移动权限类结果，例如 `Movable` / `Immovable` / `Rooted` 方向的最终 Component 或 marker result
- Excluded:
  - `PositionComponent`
  - `DirectionComponent`
  - `PlayerControlComponent`
  - `ColliderComponent`
  - `PortConnectorComponent`
  - `AutoMoveComponent.LastMoveTick` runtime counter

`AutoMoveComponent` 的存在性可以由 resolver 合成，但 `LastMoveTick` 这类 tick 运行值仍属于规则执行状态，不作为 static/runtime source 合成输入。

## Migration Plan
1. 建立 runtime effect 数据模型和 store，但不接入 Debug UI。
2. 建立 effect lifecycle 边界，处理 active / removed / expired。
3. 建立 resolver 的 source-aware component result 模型。
4. 将 included components 的静态来源接入 resolver。
5. 将 temporary runtime effect 来源接入 resolver。
6. 为 dynamic port 定义第一阶段 union merge policy。
7. 为 movement permission 定义 final result 和 Rules 读取路径。
8. 让 resolver 应用 final result 到 `GameWorld`，并触发 dirty / snapshot / delta。
9. 确认现有 Rules 仍只读取 `GameWorld` final component。
10. 扩展 snapshot/delta 和客户端镜像，使客户端看到的是 final result。
11. 用 Unity TestFramework EditMode 覆盖 source merge、remove、dynamic port、rules boundary 和 client mirror。

## Risks / Trade-offs
- Risk: 过早把所有 Component 都纳入 resolver。
  - Mitigation: 第一阶段只允许三种存在性 / 能力型 component；commit 值结果保持在规则管线。
- Risk: Ability 作为空抽象回流。
  - Mitigation: 本阶段不引入 Ability，只保留 `AbilityKind -> ComponentKind` 禁止检查。
- Risk: 动态 port 被实现成临时 `SetComponent`。
  - Mitigation: spec 和测试要求 static port + runtime port 合成，并验证 runtime port 移除后 static port 保留。
- Risk: 可移动 / 不可移动被放进 effect store 或 Ability 判断。
  - Mitigation: spec 和测试要求 Rules 只读取 final Component result 做 accepted / rejected / interrupted 仲裁。
- Risk: 客户端为了显示效果本地解析 runtime effect。
  - Mitigation: 客户端 spec 明确只能应用服务端 final result，EditMode 测试覆盖 `ClientMapWorld` 不持有本地 runtime resolver。
- Risk: Snapshot / Delta 字段继续硬编码导致新 final component 不同步。
  - Mitigation: 第一阶段同步审查 `EntitySnapshot`、`.proto` 和 `ClientMapWorld.ApplySnapshot`，只为 included components 增加最小字段。

## Stop Conditions
- 出现 `AbilityKind -> ComponentKind` 直接映射时停止。
- 为了当前阶段新增 Ability 系统时停止。
- 出现 `RuntimeEffectStore.SetComponent` 或等价 API 时停止。
- 出现 `EffectApplicationRegistry` 直接调用 `world.SetComponent(...)` 时停止。
- 出现 System / Rules 查询 `AbilityKind`、`EffectKind` 或 `RuntimeEffectStore` 决定规则时停止。
- 无法解释 static Blocking + runtime Blocking 移除 runtime 后 Blocking 仍存在时停止。
- 无法解释 static Port + runtime Port 移除 runtime 后 static Port 仍存在时停止。
- 试图把 `PositionComponent` / `DirectionComponent` 纳入第一阶段 resolver 时停止。
- 把可移动 / 不可移动仲裁交给 Ability 而不是 final Component + Rules 时停止。

## Open Questions
- 当前 IDE 中打开的旧 subgoal 文档在工作树中已删除；本提案只以 `docs/Goal/goal.md` 和当前代码/spec 为依据。
- 第一阶段 runtime effect 的调试入口可以先由 EditMode test 直接构造，不要求立刻进入 Play Mode Debug UI。
- 第一阶段 dynamic port merge policy 暂定 union；如果需要 override 或优先级，另开设计。
