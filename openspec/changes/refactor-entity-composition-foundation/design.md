## Context
当前 Shared GameCore 已经承载统一实体、组件、移动规则、空间索引、dirty 和 snapshot/delta，但实体组合仍在 `GameWorld.AddEntity(EntityConfig)` 内部硬编码。`EntityConfig` 也通过 `HasX` 字段表达组件集合，这会让后续接入 Luban 后仍然变成“表字段驱动 GameWorld if 链”，没有真正建立组合系统边界。

另一个约束是当前项目已有 active change `migrate-map-spatial-to-gamecore`，它处理空间索引迁移，不处理 `EntityConfig` 组件列表化。因此本变更只聚焦实体组合底座，避免扩大到网络同步或空间迁移。

## Goals
- `GameWorld` 不再根据 `EntityConfig.HasX` 组装组件。
- 实体组合由 archetype/config/builder 层负责。
- 服务端和客户端共享同一套组合语言，仍保持 `DG.GameCore` 不依赖 Fantasy 和 Unity。
- 本变更直接接入 Luban，实体 archetype、world spawn、player spawn rule 由 Luban 表导出。
- Luban 生成的 `cfg.Tables` 通过薄 provider adapter 进入 GameCore，不把 Luban 表类型泄漏到 `GameWorld`。
- 第一版保持显式组件枚举，不引入反射式动态组件系统。

## Non-Goals
- 不做完整组件序列化协议。
- 不引入运行时反射创建组件。
- 不修改移动裁决规则本身。
- 不执行 Unity build。

## Decisions

### Decision: `GameWorld` 只保存和索引，不负责组合
`GameWorld` 保留 `AddEntity(GameEntity)`、`SetComponent<T>`、`RemoveComponent<T>`、`FlushDelta()` 等底层能力。实体应该有哪些组件，由外部 builder/applier 决定。

Alternatives considered: 保留 `AddEntity(EntityConfig)`，只把 `DefaultWorldConfig` 改成读表。拒绝，因为这会把硬编码 if 链留在 `GameWorld`，新增组件仍需要修改世界容器。

### Decision: 第一版使用显式 `ComponentKind`
第一版用 `ComponentKind` 表达可组合组件集合，例如 `Position`、`Direction`、`Collider`、`Blocking`、`Bouncable`、`AutoMove`、`PlayerControl`。builder 根据 kind 创建组件。

Alternatives considered: 使用反射或 `object` 组件 payload。暂不采用，因为当前组件种类少，显式枚举更容易测试和维护，也更适合先接 Luban。

### Decision: 区分 archetype 与 spawn/build spec
archetype 表达“这种实体由哪些组件和 tag 组成”，spawn/build spec 表达“这个实例的 entity id、位置、方向、playerId、interval 等实例参数”。

这能把配置拆成两类：
- `EntityArchetype`: 稳定组合定义，可由 Luban 表生成。
- `EntitySpawnSpec` 或 `EntityBuildSpec`: 实例生成参数，可来自地图、服务端分配或测试。

### Decision: 本阶段直接接入 Luban
本变更新增 Luban 配置工程，至少覆盖实体组合底座需要的三类表：

- `EntityArchetype`: `ConfigId`、`ArchetypeId`、`EntityTarget`、组件 kind 集合、tags、默认组件参数。
- `WorldSpawn`: `WorldId`、`EntityId`、`ConfigId`、初始坐标、方向和实例参数覆盖。
- `PlayerSpawnRule`: player archetype、出生起点、步进、最大尝试次数。

运行时代码直接使用 Luban 生成的 `cfg.Tables`、`TbEntityArchetype`、`TbWorldSpawn` 和 `TbPlayerSpawnRule` 作为表运行时。`LubanGameConfigProvider` 只做很薄的 adapter，把 `cfg.gamecore.*` 行转换成 `DG.GameCore` 内部模型。`GameWorld`、movement systems、spatial systems 和 snapshot 模型不直接引用 Luban 命名空间或具体表类型。

### Decision: 直接使用 Luban 表运行时，不再自生成硬编码 provider
目标不是让业务为每个物体写 `CreatePlayer`、`CreateBall`、`CreateVehicle` 这类胶水，也不是再写一个 PowerShell 把 JSON 翻译成硬编码字典。Luban 从 Excel 源表导出 JSON 的同时生成 `cfg.Tables` 和 `TbXxx` 表类，运行时保留一个很薄的 loader/provider/builder 边界。这个边界只认识组件 kind 和组件参数，不认识具体物体名字。

第一版不使用运行时反射扫描组件或自动注册组件 applier。Unity IL2CPP、热更新边界和值类型组件都会让反射方案更难验证。更稳的方式是：

- Luban 从 xlsx 源表导出 entity archetype 和 spawn 数据，并生成 `cfg.Tables` 表访问代码。
- `LubanGameConfigProvider` 包装 `cfg.Tables`，不生成 per-row C# 字典。
- 维护静态 `ComponentKind -> applier` 注册表。
- 业务新增一个只由已有组件组成的物体时，只改表，不改 C#。
- 只有新增一种新的组件类型时，才需要新增组件 struct 和对应 applier，后续也可以由代码生成模板生成。

### Decision: 配置路径由客户端和服务端组合根注入
`DG.GameCore` 不负责猜测运行目录、Unity `StreamingAssets`、仓库根目录或服务端部署目录。GameCore 只提供 `LubanConfigLoader.LoadTables(string dataDirectory)` 和 `LubanGameConfigProvider.FromDirectory(string dataDirectory)` 这种显式加载入口。

客户端组合根使用 `Application.streamingAssetsPath/GameConfig` 创建 provider，并把 provider 注入 `ClientMapWorld` / `GameWorld`。服务端组合根使用 `DG_GAMECORE_CONFIG_DIR` 或服务端进程可解析的部署目录创建 provider，并把 provider 注入权威世界和玩家实体管理器。

这样可以避免构建包里因为 GameCore 自己猜错路径导致 mirror entity 创建失败，也避免 Shared 层反向依赖 Unity 或 Fantasy 的资源加载规则。`FallbackGameConfigProvider` 只用于测试、编辑器临时验证或显式 fallback 开关，不作为正式运行路径。

### Decision: `DefaultWorldConfig` 降级为测试/fallback
`DefaultWorldConfig` 可继续提供测试用 fallback 数据，但正式服务端世界初始化和客户端配置加载不应依赖 `DefaultWorldConfig.CreatePlayer/CreateBall/CreateBlocker` 这种专用工厂。正式路径必须从 Luban 导出的配置读取 archetype、spawn 和 player spawn rule。

## Risks / Trade-offs
- 风险：一次性删除 `AddEntity(EntityConfig)` 会牵连服务端、客户端和测试。缓解：先引入 builder，再迁移调用点，最后移除或降级旧入口。
- 风险：snapshot apply 当前依赖 `EntityConfig.FromSnapshot`。缓解：第一版提供 snapshot applier 或 build spec 转换层，保持协议字段不变。
- 风险：仓库当前没有 Luban 工程。缓解：本变更新增最小 Luban 工程，只覆盖实体组合底座，不把客户端表现表和网络同步表一起纳入。
- 风险：Luban 生成代码路径需要同时服务 Shared、Server 和 Unity。缓解：`cs-newtonsoft-json` 生成的 `cfg.Tables` 放在 `Shared/DG.GameCore/Config/Generated/LubanTables`，Shared 用 Newtonsoft.Json 包，Unity 侧使用项目已有的 Newtonsoft 引用；客户端表现层配置后续单独扩展。

## Migration Plan
1. 新增最小 Luban 配置工程和实体组合 Excel 表。
2. 导出 player、ball、blocker 的 archetype、demo world spawn 和 player spawn rule。
3. 新增组合模型、`cfg.Tables` adapter、loader 和 builder。
4. 用测试证明 Luban 导出的配置可构建 player、ball、blocker 的现有组件组合。
5. 迁移服务端 `MultiplayerEntityManager` 和 `AuthoritativeMoveWorldProvider`。
6. 迁移客户端 `ClientMapWorld` 的 snapshot/apply fallback。
7. 移除或降级 `GameWorld.AddEntity(EntityConfig)` 的组合职责。
8. 将客户端配置文件复制到 `StreamingAssets/GameConfig`，服务端配置路径由组合根解析后显式传入 GameCore。
9. 保留现有 movement、dirty、spatial 行为测试。

## Open Questions
- Luban 可执行文件是直接提交到 `Tools/Luban`，还是通过外部本机安装路径调用。
- 第一版 archetype 表是否只覆盖现有 player、ball、blocker，还是同时加入 benchmark mover 的预留 kind。
- 客户端表现配置是否和 GameCore archetype 分表，还是先只使用 `ConfigId/ArchetypeId` 映射 view。
