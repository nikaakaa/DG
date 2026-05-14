## 1. Proposal Validation

- [x] 1.1 复核 `openspec list`，确认本变更与 `enforce-data-driven-behavior-policy` 和 `refactor-authoritative-world-data-oriented-storage` 的边界。
- [x] 1.2 复核 `data-driven-runtime-actions`、`claim-driven-action-arbitration`、`shared-gamecore-entity-rules`、`atomic-action-behavior-layer` 当前 requirements。
- [x] 1.3 复核 `ActionSpecs.cs` 中 `ActionArbiter`、`BlockedResultResolver`、`ActionSpecRegistry`、`ActionClaim` 的现状。
- [x] 1.4 运行 `openspec validate refactor-action-policy-pipeline --strict --no-interactive`。

## 2. Architecture Boundary

- [x] 2.1 定义 `Action Policy Pipeline` 的阶段边界：intake、spec lookup、tag gate、subject、target、strategy、claim arbitration、planning、commit。
- [x] 2.2 明确普通新增行为只能通过 Luban `ActionSpec` / policy 数据 / 测试接入。
- [x] 2.3 明确新增底层策略能力通过 strategy registry 和独立策略类接入。
- [x] 2.4 明确中央仲裁器只处理候选 action unit 的 claim、priority、merge、interrupt、conflict。
- [x] 2.5 明确 tag/component/world fact 只作为过滤事实和 condition 输入，不作为隐藏行为策略语言。
- [x] 2.6 明确统一 action unit 状态机拥有生命周期状态，策略模块不得自建隐藏等待链。
- [x] 2.7 明确策略类用特性声明注册元数据，编辑器/Roslyn 生成显式注册代码，运行时不做反射扫描。

## 3. Pipeline Refactor Plan

- [x] 3.1 拆分 `ActionSpec`、policy model、request model、arbitration execution 到独立文件边界。
- [x] 3.2 建立 `ActionUnitStateMachine` 或等价模块，集中管理 queued、ready、candidate、accepted、rejected、planned、committed、deferred-output、completed。
- [x] 3.3 建立 `ActionTagGate` 或等价模块，消费 required/blocked/source/ability/state tags。
- [x] 3.4 建立 `ActionSubjectSelector` 或等价模块，消费 `ActionSpec.SubjectKind` 和 connected body view。
- [x] 3.5 建立 `ActionTargetSelector` 或等价模块，消费 `ActionSpec.TargetRule` 和 request runtime input。
- [x] 3.6 建立可注入的 `ActionStrategyRegistry` 或等价模块，按 typed strategy / primitive key 选择策略，并允许运行时执行系统接收外部注册表。
- [x] 3.7 增加策略注册特性，例如 `ActionStrategyAttribute`，用于声明 strategy key / primitive key / 构造方式。
- [x] 3.8 增加编辑器生成入口，扫描策略特性并生成显式 `GeneratedActionStrategyRegistration` 代码。
- [x] 3.9 增加等价编译期生成规则入口，复用同一套注册规则和诊断；独立 Roslyn Source Generator 项目未作为本次必要落点。
- [x] 3.10 生成代码必须只包含显式 registry 注册，不包含 world-state 决策、action id 分支、tag 条件判断、planning 或 commit 逻辑。
- [x] 3.11 将生成后的 registry 装配入口注入服务端权威执行系统、Unity EditMode 测试和 server verification 场景。
- [x] 3.12 将现有 move 行为收敛为一个 move strategy，不按普通 action id 分支。
- [x] 3.13 将 spawn/remove/debug 行为通过 strategy 或明确 primitive 模块接入，避免 `StateDrivenRules` 中业务分支继续扩大。
- [x] 3.14 将 blocked result branch 匹配与 outcome 执行拆出中央 arbiter。
- [x] 3.15 将 deferred output builder 保持为结构化输出，不恢复 parent-child pending chain。
- [x] 3.16 如果未来需要等待语义，只允许作为统一状态机显式状态扩展，并要求独立 OpenSpec。

## 4. Atomic Behavior And Composition Boundary

- [x] 4.1 保持一个 action unit 只声明自己的有限 claims。
- [x] 4.2 保持 multi-contact fanout 在发现阶段保留接触多样性，按 resolved subject/body identity 去重。
- [x] 4.3 保持 same-tick 组合只处理当前 tick ready contributions，不求解未来链。
- [x] 4.4 保持 closed-loop / feedback 通过 deferred output 跨 tick 继续。
- [x] 4.5 禁止重新引入 `PendingRuleStates`、`PendingActionState`、`PendingActionUnit` 作为 push 默认路径。
- [x] 4.6 若未来需要等待型 action 生命周期，要求独立 OpenSpec，不得复用旧 pending chain。

## 5. Luban And Config Boundary

- [x] 5.1 扩展或确认 Luban action policy 可表达 strategy key / primitive / tag gate / subject / target / blocked / deferred / claim / plan / commit。
- [x] 5.2 确认 `LubanActionSpecRegistry` 只在导入边界转换配置，不进入权威规则决策。
- [x] 5.3 增加配置校验：普通行为缺失 strategy/policy 时 fail fast。
- [x] 5.4 增加配置校验：普通行为不能引用不存在的 strategy key、blocked policy 或 handoff spec。
- [x] 5.5 增加配置案例：两个不同 action id 复用同一 strategy 和 policy 时行为等价。
- [x] 5.6 增加配置案例：新增底层 strategy key 或 primitive 时必须有注册模块、运行时注入入口和测试。
- [x] 5.7 增加生成校验：策略特性 key 与 Luban `ActionSpec` strategy/primitive 引用一致。
- [x] 5.8 增加生成校验：重复 strategy key、缺少构造入口、生成文件过期时 fail fast。

## 6. Automated Tests

- [x] 6.1 EditMode：新增普通行为只改 Luban 配置和测试，不改中央仲裁流程。
- [x] 6.2 EditMode：两个不同 action id 使用相同 strategy/policy 时产生等价 action unit、claim 和结果。
- [x] 6.3 EditMode：改变 policy 字段改变结果，不修改 strategy 类和中央仲裁流程。
- [x] 6.4 EditMode：策略类特性能生成显式 registry 注册代码。
- [x] 6.5 EditMode：生成注册代码能通过注入的 registry 进入真实 `StateDrivenRuleExecutionSystem` tick 链路，中央仲裁器和执行系统不新增玩法分支。
- [x] 6.6 EditMode：运行时 tick 路径不使用反射扫描策略特性或按类名推断策略。
- [x] 6.7 EditMode：tag gate 只过滤 required/blocked/state/ability，不通过 tag 组合推断完整行为。
- [x] 6.8 EditMode：核心规则模块扫描不包含普通行为 action-name 分支。
- [x] 6.9 EditMode：`PendingRuleStates` / `PendingActionState` / `PendingActionUnit` 不存在于 push 默认路径。
- [x] 6.10 EditMode：multi-contact fanout 保留 distinct subject，并折叠 same resolved subject。
- [x] 6.11 EditMode：closed-loop / feedback 不形成同 tick 无限求解。
- [x] 6.12 EditMode：action unit 生命周期状态只能由统一状态机推进，策略模块不能直接写隐藏等待/完成状态。
- [x] 6.13 EditMode：新增 strategy 模块不修改 `StateDrivenRules` / `ActionArbiter` 的普通行为分支，测试必须证明自定义 registry 能驱动一条真实行为结果。
- [x] 6.14 EditMode：编辑器生成与共享生成规则输出语义一致，至少覆盖同 key 排序和重复 key 诊断。

## 7. Server Verification

- [x] 7.1 Server verification：player move、auto move、mechanism push、configured wind push 仍通过。
- [x] 7.2 Server verification：新增配置行为由服务端 strategy/policy 裁决。
- [x] 7.3 Server verification：新增策略类行为通过生成注册代码和注入 registry 裁决，并进入服务端权威 tick 结果。
- [x] 7.4 Server verification：push/deferred output 不恢复旧 pending chain。
- [x] 7.5 Server verification：双向/闭环反馈跨 tick 运行且不会冻结。

## 8. Validation

- [x] 8.1 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 8.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 8.3 运行相关 Unity TestFramework EditMode 测试。
- [x] 8.4 运行 `openspec validate refactor-action-policy-pipeline --strict --no-interactive`。

## 9. Manual Verification

> 由用户手动执行，不要求 AI 代替完成。

- [ ] 9.1 在 Unity Play Mode 中连接服务端权威路径。
- [ ] 9.2 启动两个客户端并注册 observer。
- [ ] 9.3 触发一个只通过 Luban 配置接入的普通行为。
- [ ] 9.4 触发一个通过特性声明、生成注册代码、新增 strategy 类接入的底层策略行为。
- [ ] 9.5 确认两个客户端收到同一份服务端 `WorldDelta`。
- [ ] 9.6 确认客户端没有本地裁决新增行为的策略、claim、handoff、blocked result 或最终权威坐标。
