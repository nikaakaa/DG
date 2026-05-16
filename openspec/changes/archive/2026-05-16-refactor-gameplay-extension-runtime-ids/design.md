# 设计：玩法扩展运行时 ID 边界

## Context
当前架构已经有 `ActionStrategyId`、`ComponentId`、`ComponentResultId`、`EffectSpecId` 等稳定 id 雏形，也有 action strategy、commit handler、component applicator、component result resolver 的注册表。但仍存在几个核心残留：

- `ActionPrimitive` 仍出现在 strategy 元数据、ActionSpec 构造和生成逻辑中。
- `CommitProposalKind` 仍是 commit handler 的键。
- `ComponentKind` 仍是 archetype 兼容和部分 applicator API 的入口。
- `ComponentResultKind` 仍参与 result id 构造和 source contribution。
- `EffectKind` / `RuntimeEffectKind` 仍决定 effect payload 到 component contribution 的映射。
- `ActionTargetRule` 仍是 legacy targeting 的中心映射入口。

这些 enum 可以短期存在，但不能再承担玩法扩展入口。最终架构要把“新增玩法能力”从“改 enum + 改 switch”变成“新增 id 配置 + 小模块 + 注册 + 测试”。

## Goals
- 新增 action strategy 不修改 `ActionPrimitive`。
- 新增 target selector 不修改 `ActionTargetRule` 或默认中心 selector 分支。
- 新增 commit 语义不修改 `CommitProposalKind`。
- 新增静态 component applicator 不修改 `ComponentKind`。
- 新增 runtime component result 不修改 `ComponentResultKind`。
- 新增 effect payload 不修改 `EffectKind` 或 `RuntimeEffectKind`。
- 新增 snapshot/delta component payload 不修改中心 snapshot enum 或中心 if/switch。
- 保持 Shared GameCore 纯 C#，不依赖 UnityEngine、Fantasy 或运行时反射扫描。

## Non-Goals
- 不删除所有 enum。稳定内部状态仍可保留 enum，例如方向、生命周期、固定错误码、持续时间策略、堆叠策略等。
- 不把 readable id 当作规则层字符串分支。配置名必须在 provider/registry 边界解析为稳定 id。
- 不把所有行为做成 effect。移动、交换、生成、删除、方向更新仍走 action、claim、planning、commit 边界。

## Decisions

### Decision: 玩法扩展点全部使用 stable runtime id
新增能力统一使用稳定 id 作为注册键，包括：

- `ActionStrategyId`
- `TargetSelectorId`
- `CommitHandlerId` 或等价 commit proposal type id
- `ComponentId`
- `ComponentResultId`
- `EffectPayloadId`
- `SnapshotPayloadId`

正式 Luban 源表、正式生成配置、普通测试和新增能力不得继续使用玩法扩展 enum。旧 enum 只能在本变更的兼容迁移代码或兼容专项测试中短期出现，并且迁移完成后不再作为正式 authoring 字段、fallback provider 普通构造入口或新增测试入口。规则层和正式新增能力不得要求新增 enum 成员。

### Decision: 本变更内清理正式 Luban 扩展 enum 字段
本变更内直接把 Luban 源表中的扩展 enum 字段迁移为 id 字段，不再采用“先兼容、以后再清”的路线。允许保留 enum 的字段必须满足两个条件：

- 它表达稳定内部大类或状态，例如方向、生命周期、持续时间策略、堆叠策略、移除策略、固定错误码、固定排序策略。
- 它不是 action strategy、target selector、commit handler、component applicator、component result resolver、effect payload mapper、snapshot payload projector 的注册入口。

`ActionPrimitive`、`ActionTargetRule`、`CommitProposalKind`、`ComponentKind`、`ComponentResultKind`、`EffectKind`、`RuntimeEffectKind` 这类可扩展玩法入口不得作为正式 Luban 字段保留。

### Decision: fallback provider 不再作为 enum 普通测试后门
Fallback provider 的普通构造路径必须使用 id-first 数据。旧 enum helper 只能放在兼容专项测试或迁移 fixture 中，用于证明旧数据能被迁移到 id；新增普通测试、示例行为、运行时测试和扩展点测试不得再用 enum 构造玩法能力。

### Decision: commit proposal 从 enum kind 迁移为 typed id + payload
`CommitProposal` 保留排序、source、entity、tick 等通用元数据，但 commit 类型选择由 stable id 决定。不同 commit handler 声明自己的 id 和 payload 读取约定。现有 move、set direction、create、delete、auto tick、add/remove runtime effect、set component result、add/remove tag、clear runtime sources 都迁移到内置 id。

迁移后新增 `swap_entities` 这类提交语义只需要新增 handler、payload、注册和测试，不修改中心 enum。

### Decision: effect payload 不再由 EffectKind/RuntimeEffectKind 决定
`EffectSpec` 使用 `effect_payload_id` 或等价 payload id 声明效果贡献类型。payload mapper 将 `EffectSpec + EffectApplication` 转换为 runtime effect payload 或 source contribution。`RuntimeEffectSpec` 保存 payload id 和 payload 数据，不再靠 `RuntimeEffectKind` 被 resolver switch。

现有 blocking、auto move、pushable、port connector、movement permission、tag 迁移为内置 effect payload mapper。

### Decision: component result source contribution 使用 result id
`ComponentSourceContribution` 以 `ComponentResultId` 作为主键，`ComponentResultKind` 只做兼容构造。新增 result 需要 resolver 和 mapper，不得修改中心 result enum。

### Decision: target selection 不新增 ActionTargetRule
`ActionSpec` 正式引用 targeting policy id。`ActionTargetRule` 仅用于旧配置映射。新增十字、半径、射线、邻接实体等目标算法必须通过 target selector id + selector 注册实现。

### Decision: 注册是显式生成或显式组合
工具期可以扫描 metadata 生成注册源码；运行时不做程序集扫描。测试和服务端 composition 必须使用显式注册入口。重复 id 和未知 id 必须明确失败。

## Migration Plan
1. 增加 id-first API，并把 fallback provider 普通路径迁移到 id-first 构造。
2. 迁移内置 action strategy、target selector、commit handler、component applicator、component result resolver、effect payload mapper、snapshot projector 到 id 注册。
3. 更新 Luban authoring 字段，正式源表删除扩展 enum 字段，正式配置只写 id。
4. 只在兼容专项测试中保留旧 enum 到 id 的迁移验证。
5. 添加防回流测试：新增测试能力时不修改扩展 enum、不修改中心 switch、不使用 fallback enum helper。
6. 清理规则层对扩展 enum 的依赖；enum 文件如需短期保留，只能服务兼容 fixture，不得被正式配置、普通测试或新增能力引用。

## Risks / Trade-offs
- 迁移触及配置、测试和多个注册表，容易一次性改太大。任务拆成 action、targeting、commit、component、effect、snapshot 六条线，每条线都有独立测试。
- 旧测试大量直接构造 enum，需要迁移到 id-first helper。兼容 helper 只允许兼容专项测试使用，新增普通测试必须走 id。
- 本变更内删除正式 Luban 扩展 enum 字段会扩大改动面。用小任务拆分和回归测试控制风险，但不再把清理留到后续变更。
