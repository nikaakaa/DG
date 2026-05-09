## Context
DG 当前已经有 Shared GameCore 的 state-driven action / intent / plan / commit 管线，也已经有 `IntentArbiter`、`RulePlanner`、`ConflictResolver` 等边界。但当前运行时行为仍把来源和行为混在同一个枚举里，`StateDrivenRuleExecutionSystem` 会按 `PlayerMove`、`AutoMove`、`MechanismPush`、`DebugMove` 做分支。

用户目标不是简单移动分支位置，而是建立配置层和运行时层分离、完全数据驱动、仲裁层可扩展的行为运行时。新增风场、陷阱、冰面滑行、机关拉动等普通行为时，不应修改核心 system / arbiter / planner 的分支代码。

进行中的 `refactor-static-runtime-component-results` 负责 `static source + runtime source -> final Component result`。本设计从 final Component result 之后开始：action 请求如何进入、如何形成 claim、如何仲裁冲突和打断、如何生成 plan / commit。

## Goals / Non-Goals
- Goals:
  - 分离配置层 `ActionSpec` 和运行时层 `ActionRequest`。
  - 用数据描述 source、priority、tag/component 条件、claim、conflict、interrupt、merge、plan、commit。
  - 让仲裁层解释数据，而不是识别业务行为名。
  - 让执行层只消费统一 plan / commit proposal。
  - 迁移现有玩家移动、自动移动、机关推动、调试移动到统一 `Move` action 语义。
  - 用 Unity TestFramework EditMode 覆盖 schema、仲裁和兼容行为。
- Non-Goals:
  - 不在本提案阶段写实现代码。
  - 不引入 Ability 作为当前解法。
  - 不把所有规则都做成脚本语言。
  - 不让客户端承担服务端权威仲裁。
  - 不要求新增底层原语零代码扩展。

## Layer Model
```text
Luban / fallback ActionSpec 配置
        |
        v
ActionSpecRegistry
        |
        v
外部输入 / auto tick / mechanism trigger / debug / future runtime source
        |
        v
ActionRequest
        |
        v
ActionArbiter
  - condition
  - claim
  - conflict group
  - priority
  - merge
  - interrupt
        |
        v
ActionPlanner
        |
        v
CommitProposal / ConflictResolver / CommitResolver
        |
        v
GameWorld final state / Dirty / Delta
```

## Data Model Decisions
- Decision: `ActionSpec` 是配置层静态定义。
  - Rationale: 行为规则、来源默认值、claim、冲突和打断策略必须可配置，新增普通行为不能扩散到核心 system 分支。
- Decision: `ActionRequest` 是运行时实例。
  - Rationale: 一次请求只携带 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params，不复制静态策略。
- Decision: 行为原语和来源分离。
  - Rationale: `Move` 是行为原语；`Player`、`Auto`、`Mechanism`、`Debug` 是来源。当前 `PlayerMove`、`AutoMove`、`MechanismPush`、`DebugMove` 都应映射到 `Move` + source / policy。
- Decision: claim 是仲裁层的通用语言。
  - Rationale: 移动占格、同 body 冲突、目标格独占、pending push 链推进都应先表达为 claim，再由统一规则裁决。
- Decision: conflict / interrupt / merge policy 数据化。
  - Rationale: 新增普通行为时只需要选择或组合已有策略，例如 `ExclusiveTargetCell`、`SameBodyMergeIfSameClaim`、`HigherPriorityInterruptsLower`。
- Decision: 底层原语 registry 允许代码扩展。
  - Rationale: 完全数据驱动不等于无需任何代码。已有 primitive 能覆盖的普通行为应配置化；新增全新 primitive 才需要扩展 registry 和测试。

## Proposed Schema
```text
ActionSpec
- SpecId
- Primitive
- DefaultSource
- DefaultPriority
- RequiredTags
- BlockedTags
- RequiredComponents
- BlockedComponents
- ClaimRules
- ConflictPolicy
- InterruptPolicy
- MergePolicy
- PlanRule
- CommitRules
- ResultPolicy

ActionRequest
- ActionId
- SpecId
- SourceEntityId
- TargetEntityId
- TargetCoord
- Direction
- SourceStateId
- RuntimeParams
- CreatedTick
- ReadyTick
- ClientTick

ActionClaim
- ActionId
- BodyId
- EntityId
- ClaimKind
- FromCoord
- ToCoord
- ConflictGroup
- ClaimMode
- Priority
```

## Migration Plan
1. 定义 `ActionSpec` / `ActionRequest` / `ActionClaim` 的最小 schema 和 registry。
2. 将当前 `BehaviorIntentDefinitions` 的默认 tag、priority、cancel policy 迁移为 `ActionSpec` 数据。
3. 建立 `WorldAction` 到 `ActionRequest` 的兼容 adapter，保证旧调用点先不一次性爆炸。
4. 把 `PlayerMove` 映射为 `Move + SourcePlayer + TargetCoord + one-step validation policy`。
5. 把 `AutoMove` 映射为 `Move + SourceAuto + DirectionComponent + SetAutoMoveTick commit + bounce policy`。
6. 把 `MechanismPush` 映射为 `Move + SourceMechanism + Direction + blocked tag policy`。
7. 把 `DebugMove` 映射为 `Move + SourceDebug + TargetCoord + teleport-like debug policy`，并保留调试权限边界。
8. 把 `DebugSpawn`、`DebugRemove` 映射为 `Spawn`、`Remove` primitive。
9. 将 claim 生成移入仲裁层，避免 execution system 按具体行为名提前判断冲突。
10. 收敛 `StateDrivenRuleExecutionSystem`，让它编排行为管线而不是按业务 action kind 分类。
11. 补齐 Unity TestFramework EditMode 和服务端规则验证。

## Validation Strategy
- OpenSpec:
  - `openspec validate refactor-data-driven-runtime-actions --strict --no-interactive`
- Unity TestFramework EditMode:
  - 验证 `ActionSpecRegistry` 解析现有行为配置。
  - 验证玩家移动、自动移动、机关推动、调试移动迁移后结果不变。
  - 验证新增一个配置化 `Move` 行为时不修改核心 arbiter / execution system 分支。
  - 验证同 body 冲突、同 claim 合并、高优先级打断低优先级、blocked tag / final component 条件。
- 服务端规则验证:
  - 运行 `Server/Tests/AuthoritativeMoveVerification` 等价用例，覆盖现有行为保持一致。
- 手动端到端:
  - 用户在 Unity Play Mode 中启动服务端权威路径。
  - 双客户端观察玩家移动、自动移动、机关推动的 WorldDelta 同步。
  - 新增配置化普通行为后，观察服务端裁决和 observer 客户端一致。

## Risks / Trade-offs
- Risk: 一次性把所有行为做成复杂脚本语言。
  - Mitigation: 第一阶段只做固定 primitive + 数据化 policy，不做脚本 VM。
- Risk: 与 runtime component result 边界混淆。
  - Mitigation: final Component result 由 `ComponentStateResolver` 产生；本层只读取 final Component result 做行为条件和仲裁。
- Risk: 为了追求数据驱动，把底层原语也强行配置化。
  - Mitigation: 允许 primitive registry 代码扩展，但新增普通行为不能改核心流程。
- Risk: 兼容迁移期间旧 `WorldActionKind` 和新 `ActionSpec` 双轨并存。
  - Mitigation: 先建 adapter，测试覆盖等价行为，再逐步收敛旧 kind。
- Risk: 调试行为绕过数据驱动规则。
  - Mitigation: Debug 行为也必须有 spec，但可以使用 Debug source、Debug priority 和 Debug-only plan/commit policy。

## Stop Conditions
- 新增普通行为时需要修改 `StateDrivenRuleExecutionSystem` 的业务分支则停止。
- 新增普通行为时需要修改 `IntentArbiter` 的业务 kind 分支则停止。
- `ActionRequest` 复制大量 `ActionSpec` 静态策略则停止。
- 仲裁层直接查询 `RuntimeEffectStore`、`AbilityKind` 或 `EffectKind` 决定规则则停止。
- 客户端本地决定服务端权威冲突或打断结果则停止。
- sandbox JSON 被当作正式配置源则停止。

## Open Questions
- 第一阶段正式配置源可先使用 Shared fallback registry + 后续 Luban 表，还是必须本阶段同步接入 Luban Excel。
- `DebugMove` 是否继续保持“调试传送”语义，还是拆成 `DebugTeleport` primitive 以避免和普通 `Move` 的一步限制混淆。
- 第一阶段是否只迁移移动类行为，`Spawn` / `Remove` 只定义 schema 和 adapter，等下一阶段再完全收敛。
