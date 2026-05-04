## 1. 现状确认
- [x] 1.1 列出当前所有实体组合入口：`GameWorld.AddEntity(EntityConfig)`、`EntityConfig.FromSnapshot`、`DefaultWorldConfig.Create*`、`ClientMapWorld.CreateDefaultConfig`、`MultiplayerEntityManager.Join`。
- [x] 1.2 确认 active change `migrate-map-spatial-to-gamecore` 已完成或不会同时修改同一段 `GameWorld` 组合代码。

## 2. Luban 配置工程
- [x] 2.1 新增最小 Luban 工程目录，包含 schema、Excel data、生成配置和导出脚本。
- [x] 2.2 定义 `ComponentKind` 配置枚举，覆盖现有 `Position`、`Direction`、`Collider`、`Blocking`、`Bouncable`、`AutoMove`、`PlayerControl`。
- [x] 2.3 定义 `EntityTarget` 配置枚举，保持 player、monster、object 与 GameCore target 值一致。
- [x] 2.4 定义 `EntityArchetype` 表，包含 `ConfigId`、`ArchetypeId`、`EntityTarget`、组件 kind 列表、tags 和默认参数。
- [x] 2.5 定义 `WorldSpawn` 表，包含 `WorldId`、`EntityId`、`ConfigId`、位置、方向和实例参数覆盖。
- [x] 2.6 定义 `PlayerSpawnRule` 表，包含 player config、出生起点、步进和最大尝试次数。
- [x] 2.7 写入 player、ball、blocker、demo world spawn 和默认 player spawn rule Excel 数据。
- [x] 2.8 运行 Luban 导出，确认服务端和 Unity 客户端可引用生成的 JSON 与 `cfg.Tables` 表代码。

## 3. 组合模型
- [x] 3.1 新增运行时 `ComponentKind` 映射，覆盖现有 `Position`、`Direction`、`Collider`、`Blocking`、`Bouncable`、`AutoMove`、`PlayerControl`。
- [x] 3.2 新增 `EntityArchetype` 运行时模型，包含 `ConfigId`、`ArchetypeId`、`EntityTarget`、组件 kind 列表和 tags。
- [x] 3.3 新增 `EntitySpawnSpec` 或 `EntityBuildSpec`，包含 `EntityId`、`ConfigId`、位置、方向、playerId、auto move interval 等实例参数。
- [x] 3.4 新增 Luban 生成的 `cfg.Tables` 表运行时，作为正式运行时配置入口。
- [x] 3.6 新增薄 `LubanGameConfigProvider` adapter，将 `cfg.Tables` 转换为 GameCore 内部模型，不再用 PowerShell 生成硬编码 provider。
- [x] 3.5 保留测试专用 fallback provider，但正式服务端和客户端不依赖它。

## 4. Builder / Applier
- [x] 4.1 新增 `EntityBuilder`，根据 Luban provider 返回的 archetype 和 build spec 创建 `GameEntity`。
- [x] 4.2 新增组件应用流程，把 builder 输出的组件写入 `GameWorld.SetComponent<T>`。
- [x] 4.3 新增静态 `ComponentKind -> applier` 注册表，不使用运行时反射扫描。
- [x] 4.4 确保 builder 不引用 Fantasy、UnityEngine 或协议生成类型。
- [x] 4.5 确保 `GameWorld` 不需要知道 player、ball、blocker 这些业务组合。

## 5. GameWorld 收敛
- [x] 5.1 移除或降级 `GameWorld.AddEntity(EntityConfig)` 的组件组装职责。
- [x] 5.2 保留 `GameWorld.AddEntity(GameEntity)`、`SetComponent<T>`、空间索引、dirty、snapshot/delta 能力。
- [x] 5.3 调整 `AddOrUpdateEntity` 和 `ApplySnapshot`，使 snapshot apply 通过 builder/applier 或专用 snapshot applier 完成。
- [x] 5.4 确认新增由已有组件组成的物体时不需要修改 `GameWorld` 的实体创建 if 链。

## 6. 默认配置与正式配置切换
- [x] 6.1 将 `DefaultWorldConfig` 改为测试/fallback provider，提供 player、ball、blocker archetype 和 spawn spec。
- [x] 6.2 确认正式运行代码依赖 Luban provider/registry，而不是直接调用 `DefaultWorldConfig.CreatePlayer/CreateBall/CreateBlocker`。
- [x] 6.3 确认新增由已有组件组成的物体只改 Luban 表，不写 per-archetype C# 胶水。
- [x] 6.4 确认运行时不依赖反射扫描组件或反射注册 applier。

## 7. 服务端迁移
- [x] 7.1 `MultiplayerEntityManager.Join` 通过 Luban provider + builder 创建 player entity。
- [x] 7.2 `MultiplayerEntityManager` 的出生点分配使用 `PlayerSpawnRule` 表。
- [x] 7.3 `AuthoritativeMoveWorldProvider` 通过 Luban provider + builder 创建默认 demo entities。
- [x] 7.4 保持移动权限和占位规则不变。

## 8. 客户端迁移
- [x] 8.1 `ClientMapWorld` 不再按 `EntityTarget` 猜测 player、ball、blocker 组件组合。
- [x] 8.2 客户端 snapshot apply 使用服务端 snapshot 的 `ConfigId/ArchetypeId` 与 Luban provider/builder 或 snapshot applier 创建 mirror entity。
- [x] 8.3 客户端 view handle `ClientMapEntity` 不新增逻辑组件状态。

## 9. 测试
- [x] 9.1 添加 Luban 导出验证：生成结果包含 player、ball、blocker、demo world spawn 和 player spawn rule。
- [x] 9.2 添加 Shared 侧测试：Luban player archetype 生成 Position、Collider、Blocking、PlayerControl。
- [x] 9.3 添加 Shared 侧测试：Luban ball archetype 生成 Position、Direction、Collider、Blocking、Bouncable、AutoMove。
- [x] 9.4 添加 Shared 侧测试：Luban blocker archetype 生成 Position、Collider、Blocking。
- [x] 9.5 添加 Shared 侧测试：通过 builder 创建的实体仍能进入空间索引、产生 dirty、被 MovementResolveSystem 查询。
- [x] 9.6 添加服务端验证：JoinWorld 创建 player 后组件、坐标、阻挡规则与改造前一致。
- [x] 9.7 添加 Unity TestFramework EditMode：客户端 apply snapshot 后 mirror entity 组件和坐标正确。

## 10. 验证
- [x] 10.1 运行 `openspec validate refactor-entity-composition-foundation --strict --no-interactive`。
- [x] 10.2 运行 Luban 导出命令。
- [x] 10.3 运行 `dotnet build Shared\DG.GameCore\DG.GameCore.csproj -v minimal`。
- [x] 10.4 运行 `dotnet build Server\Hotfix\Hotfix.csproj --no-restore -v minimal`。
- [x] 10.5 运行 `dotnet run --project Server\Tests\AuthoritativeMoveVerification\AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 10.6 运行 `dotnet build Client\DG_Client\DG.GameCore.csproj --no-restore -v minimal /m:1`。
- [ ] 10.7 运行 Unity TestFramework EditMode。
- [ ] 10.8 手动端到端验证：启动服务端和两个客户端，Join、移动、阻挡和 snapshot 显示行为与改造前一致。
