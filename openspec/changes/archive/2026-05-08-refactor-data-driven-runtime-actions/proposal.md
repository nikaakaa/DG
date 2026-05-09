# Change: 重构数据驱动运行时行为与仲裁层

## Why
当前 `WorldActionKind`、`BehaviorIntentKind` 和 `StateDrivenRuleExecutionSystem` 仍把玩家移动、自动移动、机关推动、调试移动等来源差异写进核心规则分支。新增普通运行时行为时，容易继续修改 system / arbiter / planner 的核心代码，导致配置层、运行时层和仲裁策略混在一起。

本变更目标是建立数据驱动的运行时行为入口：配置层描述行为、claim、冲突、打断、计划和提交规则；运行时只携带一次请求的上下文；仲裁层解释配置数据；执行层只处理仲裁通过后的统一 plan / commit 结果。

## What Changes
- 新增 `data-driven-runtime-actions` 能力规格，定义 `ActionSpec`、`ActionRequest`、`ActionClaim`、冲突策略、打断策略、计划规则和提交规则的职责边界。
- 将“行为类型”和“行为来源”分离：`Move`、`Spawn`、`Remove`、`SetComponentResult`、`ApplyRuntimeEffect` 是行为原语；`Player`、`Auto`、`Mechanism`、`Debug` 是来源和策略上下文。
- 规定普通新增行为 MUST 通过新增或修改配置表达，不允许新增 `if action kind == X` 形式的核心 system 分支。
- 规定仲裁层 MUST 用数据化 claim、priority、tag/component 条件、conflict group、interrupt policy 和 merge policy 做冲突、合并、打断。
- 规定执行层 MUST 只消费仲裁通过后的统一 action / plan / commit proposal，不识别具体业务行为名。
- 规定当前已有 `PlayerMove`、`AutoMove`、`MechanismPush`、`DebugMove` 在迁移后应映射为统一 `Move` action + source / policy。
- 规定与 `refactor-static-runtime-component-results` 的边界：该变更负责 final Component 结果来源合成，本变更负责读取 final Component 结果后的 action 仲裁和执行，不把 runtime effect store 或 ability 引回 system。

## Non-Goals
- 本提案阶段不写实现代码。
- 不引入完整 Ability 系统。
- 不引入客户端预测、回滚、插值、AOI 或跨服路由。
- 不把 sandbox JSON 变成正式配置源。
- 不要求所有未来底层原语都零代码扩展；只有新增普通行为不应改核心 system，新增底层原语才允许扩展 primitive registry。

## Impact
- Affected specs:
  - `data-driven-runtime-actions`
  - 关联现有 `shared-gamecore-entity-rules`
  - 关联进行中 `runtime-component-results`
- Affected code after approval:
  - `Shared/DG.GameCore/Rules/Actions`
  - `Shared/DG.GameCore/Rules/Intents`
  - `Shared/DG.GameCore/Rules/Arbitration`
  - `Shared/DG.GameCore/Rules/Planning`
  - `Shared/DG.GameCore/Rules/Execution`
  - `Shared/DG.GameCore/Rules/Commit`
  - `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
  - `Shared/DG.GameCore/Testing/SandboxScenario.cs`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor`
- Validation:
  - `openspec validate refactor-data-driven-runtime-actions --strict --no-interactive`
  - Unity TestFramework EditMode 覆盖普通新增行为不改核心代码的配置驱动路径
  - 服务端规则验证覆盖现有玩家移动、自动移动、机关推动、调试移动行为保持一致
  - 用户手动 Play Mode 端到端验证服务端 tick、WorldDelta、双客户端同步
