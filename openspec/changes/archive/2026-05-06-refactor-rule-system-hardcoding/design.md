## Context
当前规则层已经形成 `WorldAction -> BehaviorIntent -> IntentArbiter -> RulePlanner -> ConflictResolver -> Commit` 的主路径，但代码组织仍处于过渡态：

- `BehaviorIntent` 通过 `InferSourceTag`、`InferAbilityTag`、`InferBlockedTags` 三个 `switch` 推导默认策略。
- `StateDrivenRules.cs` 单文件承载过多职责。
- `Movement/Legacy/Systems.cs` 仍定义 `MovementResolveSystem`、`AutoMoveSystem`、`PushOnEnterSystem`，并被服务端验证、Unity EditMode、本地客户端 `MovementSystem` 和服务端构造参数引用。
- 现有 specs 仍有若干 `MovementResolveSystem` 叙述，已落后的文本需要通过本变更修正为 state-driven rule system。

## Goals / Non-Goals

Goals:
- 让 intent 默认策略集中定义，新增行为时只改一个定义入口并由测试证明。
- 让 system 层文件结构表达当前三层/多层职责，而不是把所有规则塞在单个文件。
- 迁移或隔离 legacy movement resolver，避免服务端权威路径和客户端本地路径继续依赖旧裁决。
- 保持 Shared GameCore 纯 C#，不引入 Unity/Fantasy 依赖。

Non-Goals:
- 不新增完整技能系统、GAS 生命周期、冷却、属性、消耗或 GameplayCue。
- 不把规则策略立刻做成 Luban 配置表。
- 不实现客户端预测、回滚、插值或 AOI。
- 不改变 component-system 的基本判断：system 读取 component 是正常规则输入。

## Decisions

### Decision: 先用代码注册表替代 switch，不直接上配置表
新增 `BehaviorIntentDefinition` 与 registry，集中声明每个 `BehaviorIntentKind` 的 `SourceTag`、`AbilityTag`、`RequiredTags`、`BlockedTags` 和 `CancelPolicy`。未知 kind 必须失败，不能 fallback。

理由：当前规则语义还在收口，直接上 Luban 配置会过早固化数据结构。代码注册表先解决多 switch 漂移和静默默认值问题。

### Decision: 保留 component 查询，拆薄规则函数
`ProcessPlayerMove` 这类函数可以继续读取 component，但需要拆分为更小的 system helper，例如主体解析、目标校验、阻挡查询、push pending、intent 创建和结果回写。

理由：component 是消除实体类型耦合的抽象层，规则 system 依赖 component 是必要耦合；真正的问题是函数职责过厚。

### Decision: legacy 删除必须以依赖迁移为前置条件
`Movement/Legacy/Systems.cs` 不能直接删除。实现阶段必须先迁移或替换所有引用，包括服务端验证、Unity EditMode、本地 `MovementSystem` 和服务端构造参数。删除前必须有验证证明旧路径不再被运行时使用。

### Decision: 文件夹结构尽量按 system 层职责表达
目标结构可以按当前规则管线拆分，例如：

- `Movement/Actions`
- `Movement/Intents`
- `Movement/Planning`
- `Movement/Commit`
- `Movement/Systems`
- `Movement/Pending`

实际实现可以分阶段迁移，避免一次性大搬家造成 Unity csproj 引用问题。

### Decision: 客户端本地 MovementSystem 只保留为非权威辅助
本变更将客户端本地 `MovementSystem` 从服务端权威默认路径中移除或隔离。若保留，它只能用于 EditMode、sandbox local mode 或离线调试，并且命名、注册位置和测试必须明确它不是权威移动路径。

理由：服务端权威模式下，客户端移动应由 `ClientMoveNetworkSubmitter` 提交到服务端 tick，并通过 response / WorldDelta 应用最终状态。本地 `MovementResolveSystem` 不应在服务端响应前修改权威镜像坐标。

### Decision: DebugSetEntityTag 延后到独立变更
本变更不迁移 `DebugSetEntityTag` 到 tick/action 管线。它作为后续调试编辑语义统一变更处理。

理由：`DebugSetEntityTag` 涉及协议源、协议导出、handler、input queue、tick 语义、delta 同步和客户端工具。把它并入本次规则层硬编码收口会扩大 scope。当前变更只记录这个遗留风险，不假装已经统一。

## Risks / Trade-offs
- 风险：移动文件会触发 Unity 生成的 csproj 过期。缓解：先做小步迁移，Unity 侧刷新后再跑 EditMode。
- 风险：旧 `MovementResolveSystem` 的测试仍表达历史需求。缓解：先新增等价 state-driven 验证，再修改或删除旧测试。
- 风险：注册表仍是代码硬编码。缓解：本变更目标是集中硬编码和防静默 fallback，不声称完成配置化。

## Migration Plan
1. 增加 intent definition registry，并用测试锁住现有五种 intent 的 tag/cancel policy。
2. 替换 `BehaviorIntent` 内部 `Infer*` switch。
3. 拆薄 `StateDrivenRules.cs`，保持行为不变。
4. 审计 `MovementResolveSystem`、`AutoMoveSystem`、`PushOnEnterSystem` 的引用并建立 state-driven 等价路径。
5. 迁移客户端本地 `MovementSystem` 或明确降级为非权威测试辅助路径。
6. 删除或隔离 `Movement/Legacy/Systems.cs`。
7. 更新 specs 中旧 `MovementResolveSystem` 叙述。
