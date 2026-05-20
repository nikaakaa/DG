# Change: 重构动作与组件扩展边界

## Why
当前规则系统已经能通过 `ActionSpec` 和 Luban 配置组合已有动作能力，但新增底层动作、提交语义、静态组件或运行时组件结果仍需要修改中心 enum、中心注册表或中心分发逻辑。

这会让后续玩法扩展继续压到 `ActionPrimitive`、`CommitResolver`、`ComponentApplicationRegistry`、`ComponentStateResolver`、snapshot/delta 构造等核心文件上，违背“对修改关闭，对扩展开放”的目标。

## What Changes
- 将动作执行策略从 `ActionPrimitive` enum 驱动迁移为稳定的策略 id / 注册表驱动。
- 将 commit 提交执行从 `CommitProposalKind` 中心分发迁移为注册式 commit handler。
- 将静态实体组件挂载从 `ComponentKind` 中心字典迁移为组件 id / applicator 注册表。
- 将运行时组件结果合成从 `ComponentResultKind` 中心合成迁移为 source contribution id / resolver 注册表。
- 建立 component 查询、target filter、blocked condition 使用的 component id 解析边界。
- 明确 snapshot/delta 的扩展边界：新增需要同步的 component 必须通过显式 snapshot projector / applier 扩展，而不是继续把字段塞进 `GameWorld.CreateSnapshot`。
- 保留现有行为语义：玩家移动、自动移动、机关推动、debug spawn/remove/move、runtime effect 第一批结果必须保持兼容。

## Impact
- Affected specs:
  - `data-driven-runtime-actions`
  - `shared-gamecore-entity-rules`
  - `runtime-component-results`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/*`
  - `Shared/DG.GameCore/Rules/Commit/*`
  - `Shared/DG.GameCore/Configuration/*`
  - `Shared/DG.GameCore/RuntimeEffects/*`
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/Snapshots/*`
  - `Config/Luban/Defines/gamecore.xml`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor`

## Non-Goals
- 不引入第三方 ECS。
- 不改变当前权威 tick、WorldDelta、Fantasy session、客户端镜像职责。
- 不把 Unity 表现层变成规则裁决者。
- 不要求所有未来 component 自动网络同步；同步必须显式声明投影。

## Validation
- `openspec validate refactor-action-component-extension-boundaries --strict --no-interactive`
- `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode 覆盖动作策略、commit handler、component applicator、component result resolver、snapshot projector 注册扩展。
- 用户手动端到端验证：启动 Fantasy 服务端和两个 Unity Play Mode 客户端，覆盖新增测试动作、测试 component、runtime effect、snapshot/delta 镜像，确认两个客户端只跟随服务端结果。
