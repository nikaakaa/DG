# Change: 重构 Action 策略管线

## Why

当前行为层已经把部分行为差异迁移到 `ActionSpec` 和 policy 数据，但 `ActionArbiter` 仍然集中承担 target、subject、tag gate、blocked outcome、deferred output、claim 冲突和结果分支。这样新增普通行为时虽然不一定按 action 名字分支，但开发者仍需要反复理解并修改同一个中央类，扩展性没有真正达到目标。

本变更规划把行为层改成基于原子 action unit、tag/component 过滤、策略注册和 claim 仲裁的管线：普通新增行为优先只改 Luban 配置和测试；只有新增底层策略能力时才新增可注册的小策略类；中央仲裁器只选择候选和处理 claim/priority/merge/interrupt，不再承载所有行为分支。

## What Changes

- 定义 `Action Policy Pipeline` 作为 DG 行为层目标结构：intake、spec lookup、tag gate、subject/target selection、strategy build、claim arbitration、planning、commit。
- 把 `ActionArbiter` 从“中央行为解释器”收敛为“候选 action unit 与 claim 的仲裁器”。
- 引入策略注册边界：已有 primitive/policy 能表达的普通行为只通过 Luban `ActionSpec`、policy 行和测试接入。
- 新增底层策略能力时使用独立策略类或等价模块注册，不修改中央仲裁流程。
- 新增底层策略类使用特性声明注册元数据，并由编辑器工具或 Roslyn Source Generator 生成显式注册代码；运行时只依赖生成后的显式 registry，不做反射扫描或按类名推断。
- 补足 strategy registry 的运行时注入边界：新增底层策略类必须能通过注入的 registry 进入真实服务端 tick 链路，而不是只在 registry 单测里可注册。
- 引入统一 action unit 状态机，集中管理 queued、ready、candidate、accepted、rejected、committed、deferred-output、completed 等生命周期状态。
- 明确 tag/component/world fact 只作为过滤和条件输入，不能通过 tag 组合反向推断一套隐藏行为。
- 保留原子行为语义：每个 action unit 有有限 claim、有限结果、有限 deferred output；组合和反馈跨 tick 流动。
- 明确禁止复活旧 `PendingRuleStates` / parent-child pending chain，也禁止用全链硬编码求解替代原子 action unit。

## Non-Goals

- 不在本 proposal 阶段写实现代码。
- 不一次性实现完整 UE GAS。
- 不引入脚本语言、行为树、VM 或通用表达式系统。
- 不把客户端表现层变成权威行为裁决层。
- 不恢复旧 pending chain，不把 push 组合重新建成 parent 等 child 成功后 retry 的链。
- 不解决所有未来策略类型；本变更只定义扩展边界和拆分路径。

## Impact

- Affected specs:
  - `data-driven-runtime-actions`
  - `claim-driven-action-arbitration`
  - `shared-gamecore-entity-rules`
  - `atomic-action-behavior-layer`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
- `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
- `Shared/DG.GameCore/Rules/Commit/*`
- `Shared/DG.GameCore/Config/LubanActionSpecRegistry.cs`
- strategy registration attribute / generated registration output
- `Config/Luban/Defines/gamecore.xml`
- `Config/Luban/Datas/gamecore/*`
  - Unity TestFramework EditMode tests
  - Server authoritative verification

## Verification

- `openspec validate refactor-action-policy-pipeline --strict --no-interactive`
- Shared build: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Server verification: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode:
  - 普通新增行为只改配置和测试
  - 新增策略类通过 registry 接入
  - 特性声明的策略类能生成显式注册代码，生成代码可注入真实 `StateDrivenRuleExecutionSystem`
  - 运行时代码不通过反射扫描策略类
  - 中央仲裁流程不出现普通行为 action-name 分支
  - 不存在旧 pending chain 文件或调用
- 用户手动验证：
  - Play Mode 连接服务端权威路径
  - 双客户端 observer 收到一致 `WorldDelta`
  - 新增配置行为和新增策略类行为均由服务端裁决
