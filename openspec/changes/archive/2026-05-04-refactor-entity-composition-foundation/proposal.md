# Change: 重构实体组合底座

## Why
当前 `GameWorld.AddEntity(EntityConfig)` 仍通过 `HasPosition`、`HasCollider`、`HasAutoMove` 等布尔字段决定实体组件组合，导致新增组件或接入导表后仍要修改 `GameWorld` 的 if 链。项目下一步要接入 Luban，因此需要先把实体组合规则从 `GameWorld` 移到明确的 archetype/builder/config 边界。

## What Changes
- 将实体组件组合规则从 `GameWorld` 中移出，`GameWorld` 只负责 entity 存储、component store、空间索引、dirty 和 snapshot/delta。
- 新增实体组合边界：Luban archetype 表描述组件组合，Luban spawn/build 表描述实体实例参数，builder/applier 负责把组合应用到 `GameWorld`。
- 直接接入 Luban Excel 配置导出链路，源数据使用 xlsx，生成服务端和 Unity 客户端可共享的 JSON 与 `cfg.Tables` 表代码。
- 运行时代码依赖 Luban 生成的 `cfg.Tables` 和薄 provider adapter，不再依赖手写 `DefaultWorldConfig` 或自生成硬编码字典作为正式配置来源。
- 将 `DefaultWorldConfig` 降级为测试或 fallback 数据来源，不能继续作为正式实体配置来源。
- 调整服务端玩家创建、默认 demo entity 创建和客户端 snapshot/mirror apply 的组合入口，使它们不再按 `EntityTarget` 猜组件组合。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `multiplayer-entity-management`
- Affected code:
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/Config/EntityConfig.cs`
  - `Shared/DG.GameCore/Config/DefaultWorldConfig.cs`
  - `Shared/DG.GameCore/Snapshots/Snapshot.cs`
  - `Server/Hotfix/AuthoritativeMove/Infrastructure/MultiplayerEntityManager.cs`
  - `Server/Hotfix/AuthoritativeMove/World/AuthoritativeMoveWorldProvider.cs`
  - `Client/DG_Client/Assets/Scripts/Map/World/ClientMapWorld.cs`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor`
  - server verification tests under `Server/Tests/AuthoritativeMoveVerification`

## Out of Scope
- 不在本变更中实现完整网络 AOI/visible sync。
- 不在本变更中实现车辆、路线、benchmark mover 玩法或压测场景。
- 不在本变更中重做 snapshot/delta 为通用组件序列化协议。
- 不在本变更中启用 Fantasy.config 的旧 `<configTable>` 入口。
- 不在本变更中实现完整客户端表现表，只保留 `ConfigId/ArchetypeId` 到 view 配置的后续扩展边界。
