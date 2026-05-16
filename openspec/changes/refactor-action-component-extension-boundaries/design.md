# 设计：动作与组件扩展边界

## Context
当前 GameCore 已经把普通行为收敛到 `ActionSpec -> ActionRequest -> Strategy -> Arbitration -> Planning -> Commit`，也把 runtime effect 结果收敛到 final component result。但多个关键扩展点仍由中心 enum 和中心分发表控制：

- `ActionPrimitive` 决定动作策略。
- `CommitProposalKind` 决定 commit 执行。
- `ComponentKind` 决定 archetype 挂载。
- `ComponentResultKind` 决定 runtime/static source 合成。
- `GameWorld.CreateSnapshot` / `ApplySnapshot` 手写需要同步的 component 字段。

这些点导致新增底层能力时必须修改核心文件。目标不是让任意代码动态热插拔，而是把扩展面固定为小类注册和配置引用。

## Goals
- 新增普通行为时，能通过现有策略和配置完成，不修改核心规则管线。
- 新增底层动作策略时，只添加 strategy 小类、注册声明、配置和测试。
- 新增 commit 语义时，只添加 commit handler 小类、注册声明和测试。
- 新增静态 component 挂载时，只添加 component applicator 小类、配置和测试。
- 新增运行时 component result 时，只添加 resolver 小类、payload 映射、配置和测试。
- 新增需要同步的 component 时，只添加 snapshot projector/applier 小类、协议/快照 payload 声明和测试。
- 保持 Shared GameCore 纯 C#，不依赖 UnityEngine、Fantasy 或第三方 ECS。

## Non-Goals
- 不做技能系统。
- 不在运行时规则管线中用反射扫描决定行为；编辑器/工具期反射发现并生成显式注册代码是正式路径。
- 不让 tag 组合替代显式策略字段。
- 不让 runtime effect 直接移动 entity 或绕过 action commit。
- 不要求所有 component 自动同步。

## Architecture
最终架构按五个注册边界拆分。

### Action strategy registry
`ActionSpec` 消费稳定策略 id。策略 id 来自 Luban provider 的解析结果，而不是规则层比较字符串。现有 `ActionPrimitive` 可以在迁移期继续存在，但只能作为 authoring 兼容字段或默认策略映射。

策略小类声明它支持的策略 id。编辑器/工具期可以通过反射扫描 `[ActionStrategy]` 或等价 metadata，然后生成显式注册文件；server/test/client mirror composition 注入生成后的 registry。运行时规则管线不做程序集扫描，也不靠 Unity editor-only API 选择行为。

### Commit proposal handler registry
`CommitResolver` 只负责排序、共享 commit 上下文、移动冲突集合和 handler 调度。每种 proposal 由对应 handler 执行。移动 handler 可以继续使用共享移动验证服务，避免把 move 特例散落到每个 handler。

新增 proposal 不修改 `CommitResolver` 主流程，只新增 handler 和注册。

### Component applicator registry
`EntityArchetype.Components` 迁移到 component id。component applicator 负责把 spawn/archetype/provider 输入转换成 initial component 或 static source contribution。

`EntityBuilder` 只遍历 component id 并调用 registry。新增 archetype 组合继续只改配置；新增 component 类型才加 applicator 小类。

### Component fact query registry
target filter、blocked condition、debug palette search 等需要判断“实体是否拥有某 component”的地方，不再 switch `ComponentKind`。它们通过 component fact query registry 从 component id 查询 final component。

这样新增可被规则条件引用的 component 时，不需要修改 targeting 或 blocked resolver。

### Component result resolver registry
`ComponentStateResolver` 保留 source 集合、静态/运行时来源分离、确定性顺序和 dirty 标记职责。具体 final component 怎么从 sources 合成，由 component result resolver 小类处理。

runtime effect 的 payload 不直接等于 final component 写入。它先变成 result source contribution，再由 resolver 合成 final state。

### Snapshot projector / applier registry
同步不是所有 component 的默认能力。需要同步的 component 必须显式注册 projector / applier。projector 从 `GameWorld` final state 生成 snapshot payload；applier 在 mirror world 应用 payload。

`GameWorld.CreateSnapshot` 和 `ApplySnapshot` 调度 registry，而不是继续成为所有同步 component 的中心修改点。

## Data Model Direction
- action strategy id：稳定 id，authoring 可读，provider 解析后进入规则层。
- component id：稳定 id，配置和规则条件共用。
- component result id：稳定 id，runtime/static source settlement 使用。
- snapshot payload id：稳定 id，只服务同步。

迁移期可以保留旧 enum 与新 id 的映射，最终新增能力不得要求修改 enum。

## Determinism
- 注册表构建必须显式、确定性、可测试。
- 同一 id 重复注册必须失败。
- 未知 id 必须清晰失败。
- source settlement 排序继续按 entity id、source kind/id、注册顺序或显式 priority 决定，不依赖 dictionary 顺序。

## Compatibility
现有行为必须保持：

- player move
- auto move
- mechanism push
- debug move/spawn/remove
- runtime effect add/remove/expire
- static + runtime component source merge
- WorldDelta changed/removed/animation metadata

## Rollout
先做 handler/registry 外壳，再迁移现有能力，最后切换配置字段。迁移期允许旧 enum 解析到新 id，但不能把旧 enum 作为新增能力的唯一入口。

## Risks
- snapshot payload 迁移会触碰服务端和 Unity mirror，需要测试现有协议/镜像。
- Luban 字段迁移可能影响现有配置导出，需要保留兼容读取或一次性更新配置数据。
- 过度抽象可能让简单行为变复杂，所以每个 registry 只服务已存在的中心分发表，不扩展无关能力。
