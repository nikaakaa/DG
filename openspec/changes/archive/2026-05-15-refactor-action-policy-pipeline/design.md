## Context

DG 当前主线是 `WorldAction -> ActionRequest / ActionSpec -> ActionArbiter -> RulePlanner -> ConflictResolver / CommitResolver -> GameWorld -> WorldDelta`。`ActionSpec` 已经承载 primitive、source、priority、tag filter、target rule、blocked policy、handoff/deferred policy、conflict、interrupt、merge、subject、plan、commit 和 cost 等策略字段。

问题不再只是 action-name 分支。当前更大的问题是中央执行器仍然过大：`ActionArbiter` 同时负责请求解析、目标选择、主体选择、tag 检查、claim 构建、blocked 分支解释、deferred 输出、bounce/reject 和 claim 冲突。它虽然消费了数据，但还是一个集中解释器。

目标结构应更接近 UE GAS 的工程思想，但不照搬 GAS：Ability/Action 配置声明入口和限制，tag/component 作为过滤事实，策略注册提供底层能力扩展点，仲裁层只比较候选声明和 claim，最终 commit 统一落地。

## Goals / Non-Goals

Goals:

- 普通新增行为默认只改 Luban action policy 数据、blocked/deferred policy 数据和测试。
- 新增底层策略能力时新增一个小策略模块并注册，Luban 配置引用该策略，不修改中央仲裁流程。
- 中央仲裁器只处理 action unit 候选、priority、claim、merge、interrupt、conflict，不处理具体玩法分支。
- 统一 action unit 状态机管理生命周期，策略模块不得各自维护隐藏等待链或完成链。
- tag/component/world fact 作为 gate 或 condition 输入，不替代 `ActionSpec` / strategy / policy 字段。
- 原子行为保持有限：一个 action unit 只声明自己的 claims，不一次性硬解完整未来链。
- push 组合、反馈和闭环通过有限 transaction + deferred output 跨 tick 流动。
- 不复活旧 pending chain。

Non-Goals:

- 不实现完整 GameplayAbilitySystem。
- 不支持任意脚本化条件表达式。
- 不让 Unity client mirror 执行权威策略。
- 不引入全链同 tick transaction solver。
- 不把 tag 组合变成隐藏策略语言。

## Decisions

### Decision: 命名为 Action Policy Pipeline

本变更采用 `Action Policy Pipeline` 作为架构名。它表达的是：

- `Action`：运行时原子行为单元。
- `Policy`：行为差异来自 `ActionSpec` 和显式 policy 数据。
- `Pipeline`：执行不是一个大类解释所有分支，而是分阶段处理。

### Decision: 普通行为只通过配置接入

当新行为能用已有 primitive、tag gate、subject policy、target rule、blocked policy、deferred policy、claim policy、plan rule 和 commit rule 表达时，接入路径必须是：

1. 修改 Luban action policy 数据。
2. 如需要，修改 blocked/deferred policy 数据。
3. 运行 Luban 导出。
4. 增加 Unity EditMode / server verification。
5. 不修改中央仲裁流程。

### Decision: 新底层策略用注册模块接入

当现有 primitive 或 policy 无法表达新行为时，允许新增底层策略能力。新增能力必须是独立模块，例如 `MoveActionStrategy`、`PullLineActionStrategy`、`RuntimeEffectActionStrategy` 这样的领域策略模块。中央 pipeline 通过 registry 查找策略，而不是新增 `if action == xxx`。

补充决策：strategy registry 必须成为运行时执行系统的显式依赖，而不是在 `StateDrivenRuleExecutionSystem` 内部固定 new 默认 registry。新增底层策略能力时，调用方必须能够组合一个包含默认策略和新增策略的 registry，并把它注入服务端权威执行系统、测试场景或未来的配置装配入口。否则“新建类 + 配置引用”只停留在 registry 单元测试层，不能证明真实 tick 链路已经可插拔。

补充决策：短期内继续保留 `ActionPrimitive` 作为策略选择 key，但执行系统不得写死某个新增 primitive 或行为名。若未来需要多个策略复用同一个 primitive，或者需要配置直接引用 `strategy_key`，必须把 `ActionSpec` 的策略选择字段从 primitive-only 扩展为显式 strategy key，并同步 Luban schema、导入校验和测试。

### Decision: 策略注册使用特性声明 + 生成显式代码

策略类的开发体验采用特性声明，运行时采用显式生成代码。策略作者在类上声明类似 `[ActionStrategy(ActionPrimitive.ApplyRuntimeEffect)]` 或 `[ActionStrategy("runtime_effect")]` 的注册元数据；编辑器工具或 Roslyn Source Generator 扫描编译期语义模型并生成 `GeneratedActionStrategyRegistration` 之类的显式注册入口。生成结果必须是普通 C# 代码，例如 `registry.Register(new RuntimeEffectActionStrategy())`，并由服务端装配、测试装配或未来配置装配显式调用。

这个方案的边界是：

- 特性只用于开发期声明和生成输入，不作为运行时反射入口。
- 生成代码可以被 code review、编译和测试覆盖，运行时不扫描程序集、不按类名推断策略。
- `StateDrivenRuleExecutionSystem` 只接收已经装配好的 `ActionStrategyRegistry` 或等价接口，不知道特性、Roslyn、编辑器菜单或生成器实现细节。
- 生成器必须 fail fast：重复 key、缺少无参构造或工厂、策略 key 与 `ActionSpec` / Luban schema 不一致时直接报错。
- 生成文件是派生产物，不手写；如果策略列表不对，修策略类特性或生成器规则后重新生成。

优先落地路径是先支持编辑器菜单生成，保证 Unity 项目内可见、可手动触发、易调试；再补 Roslyn Source Generator，让 CI / IDE 编译期也能产出同一类显式注册代码。两者的输出语义必须一致，不能形成两套注册规则。

### Decision: 仲裁器只看候选和 claim

仲裁器不再负责“怎么构造一个 move 候选”或“blocked 后怎么生成 deferred output”。这些应由 action strategy、blocked outcome policy 和 deferred output builder 产出结构化候选或输出。仲裁器只负责：

- 排序 priority。
- 同 claim merge。
- exclusive claim 冲突。
- interrupt / reject。
- 输出 winners 和 rejected results。

### Decision: 生命周期由统一状态机管理

每个 action unit 都通过统一状态机推进。状态机负责记录 unit 是否 queued、ready、candidate-built、accepted、rejected、planned、committed、deferred-output-emitted 或 completed。策略模块不能自己保存隐藏等待状态，也不能绕过状态机直接宣布最终完成。

等待语义如果出现，必须作为统一状态机里的显式状态扩展，例如 `WaitingForOutput`、`RetryScheduled` 或 `Cancelled`，并且需要单独 OpenSpec 明确为什么不能用 deferred output 表达。默认 push、反馈、组合不走等待父子链，而是当前 unit 完成有限输出，后续 tick 由新 ready unit 继续。

### Decision: tag 是过滤事实，不是策略推理语言

Tag 可以表达来源、能力、状态、免疫、阻挡等事实。策略可以声明 required/blocked tags 或 condition tags。规则层不能通过“某几个 tag 同时存在”来反向推断一个完整普通行为。

### Decision: 原子行为不走旧 pending chain

旧 `PendingRuleStates` / parent waits child / parent retries success chain 不作为本架构扩展点。组合、反馈、闭环和长链通过有限 action unit、claim、deferred output 和跨 tick transaction 表达。任何需要等待型 action 生命周期的新语义必须另开 proposal，不能复用旧 pending chain。

## Target Pipeline

目标管线：

```text
WorldActionQueue
  -> ActionRequestAdapter
  -> ActionUnitStateMachine
  -> ActionSpecRegistry
  -> ActionTagGate
  -> ActionSubjectSelector
  -> ActionTargetSelector
  -> ActionStrategyRegistry
  -> ActionUnit candidates
  -> ActionClaimArbitration
  -> ActionPlanning
  -> Conflict / Commit
  -> GameWorld delta
```

每一层只拥有自己的职责。`ActionSpecId` 只用于查配置；普通行为差异来自配置字段和策略 registry。

## Migration Plan

1. 在 proposal 阶段固定 spec 边界和任务拆分。
2. 在 apply 阶段先增加扫描/测试，锁定当前中央类膨胀问题。
3. 引入统一 action unit 状态机并保持现有 ready/drain 行为等价。
4. 拆出只读阶段：tag gate、subject selector、target selector。
5. 拆出 strategy registry 和已有 move/spawn/remove 策略。
6. 增加策略注册特性、编辑器生成入口和生成文件边界，让策略类能生成显式 registry 装配代码。
7. 把生成后的 registry 装配入口注入服务端权威执行系统和测试场景。
8. 把 blocked result branch 的执行从中央 arbiter 中抽出为结构化 outcome。
9. 把 claim 仲裁收敛为独立系统。
10. 保持现有行为测试通过后，再删除中央类中的兼容分支。

## Risks / Trade-offs

- 风险：拆得过细导致抽象过早。
  - Mitigation：只按已有职责拆，不引入泛型框架或通用接口层。
- 风险：策略 registry 变成另一个大 switch。
  - Mitigation：registry 只按 typed key 查模块，不包含玩法分支逻辑。
- 风险：特性注册退化成运行时反射扫描。
  - Mitigation：特性只允许作为生成输入，权威 tick 链路只依赖生成后的显式注册代码和注入 registry。
- 风险：编辑器生成和 Roslyn 生成产物不一致。
  - Mitigation：两条生成路径共享同一份扫描/排序/诊断规则，测试固定生成结果和重复 key 诊断。
- 风险：tag 被误用成隐藏脚本语言。
  - Mitigation：spec 明确 tag 只能作为事实输入，策略选择仍来自显式字段。
- 风险：为了处理复杂组合又走回 pending chain。
  - Mitigation：spec 明确旧 pending chain 禁用，复杂等待语义需要独立 proposal。
