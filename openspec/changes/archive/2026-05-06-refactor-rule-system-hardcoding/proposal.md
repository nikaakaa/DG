# Change: 收口规则 system 层硬编码

## Why
当前 Shared GameCore 的行为裁决已经集中到 action / intent / plan / commit 管线，但 `BehaviorIntentKind` 的默认 tag 推导仍由多个 `switch` 维护，`StateDrivenRules.cs` 同时承担 action、intent、planning、commit 和 pending state 逻辑，`Movement/Legacy/Systems.cs` 仍被测试、客户端本地 system 和服务端构造参数引用。继续新增规则入口会扩大硬编码和旧裁决路径并存的问题。

## What Changes
- 将 `BehaviorIntentKind` 的默认 `SourceTag`、`AbilityTag`、`RequiredTags`、`BlockedTags`、`CancelPolicy` 收口到规则定义注册表，禁止未知行为静默 fallback 为玩家来源。
- 将移动规则按 system 层职责整理为 action intake、intent creation、arbitration、planning、conflict/commit、pending state 等边界，文件夹结构尽量反映这些层次。
- 审计并迁移 `Movement/Legacy/Systems.cs` 的现有依赖，在无运行路径和验证覆盖后移除或隔离 legacy 裁决实现。
- 保持 component-system 边界不变：system 可以读取 component 作为规则输入，但不得依赖具体实体类型或将规则复制到 Unity 客户端外壳。
- 保持第一阶段小重构：不引入完整 GAS、技能系统、客户端预测、AOI、回滚或新的配置表。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `client-world-runner`
- Affected code:
  - `Shared/DG.GameCore/Movement/Arbitration/BehaviorArbitration.cs`
  - `Shared/DG.GameCore/Movement/Rules/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Movement/Legacy/Systems.cs`
  - `Shared/DG.GameCore/Testing/SandboxScenario.cs`
  - `Server/Hotfix/AuthoritativeMove/*`
  - `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`
  - `Client/DG_Client/Assets/Scripts/Map/Runtime/Systems/MovementSystem.cs`
  - `Client/DG_Client/Assets/Tests/Editor/Map/*`

## Clarifications
- `Movement/Legacy/Systems.cs` 当前仍有实际依赖，不能在第一步直接删除。实现阶段必须先迁移或替换引用，再删除或隔离。
- 现有 component 查询不是本变更要消除的问题。规则 system 读取 `PositionComponent`、`BlockingComponent`、`PushableComponent` 等是必要耦合；本变更只收口行为策略硬编码和旧裁决路径。
