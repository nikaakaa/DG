# Component-System 新增与审查工作流

## 目标

新增或调整 DG component-system 时，默认走配置驱动实体组合、Shared GameCore 承载规则、服务端权威 tick 裁决、Unity 客户端只做镜像和显示。

这条工作流适合 marker component、简单参数 component、机关规则 component、调试实体能力和 sandbox 验证。复杂参数组件需要先补组件专用配置表、component parameter 表或后续统一参数 schema，不能随手把专用字段塞进 `EntityArchetype`。

当前架构状态：

- Luban XML/Excel 是正式实体和组件组合来源。
- `.proto` 是网络协议来源。
- Shared GameCore 是 runtime 规则和状态来源。
- Fantasy server 是权威 tick / handler / sync 外壳。
- Unity client 的世界运行时代码位于 `Client/DG_Client/Assets/Scripts/ClientWorld`，它是镜像、输入提交、显示和工具外壳。
- `refactor-rule-system-hardcoding` 已归档，服务端权威移动主路是 state-driven action / intent / plan / commit。
- `refactor-world-rule-folder-structure` 调整后，Shared 规则代码主入口是 `Shared/DG.GameCore/Rules`，不再是 `Shared/DG.GameCore/Movement`。

## 主路径

1. 在 `Config/Luban/Defines/gamecore.xml` 的 `ComponentKind` 中新增枚举值。
2. 在 Luban Excel 表中为目标 archetype 配置 component 列表。
3. 如 component 需要专用参数，优先新增专用配置表，例如 `port_connector_config.xlsx` 这类形态。
4. 运行 `Config/Luban/Run.ps1` 导出配置和生成代码。
5. 确认 `Shared/DG.GameCore/Config/ComponentKind.cs` 已由同步脚本更新。
6. 在 `Shared/DG.GameCore/Components/Components.cs` 新增 component 数据结构。
7. 在 `Shared/DG.GameCore/Config/ComponentApplicationRegistry.cs` 注册 `ComponentKind -> component` 应用逻辑。
8. 若只是静态能力或显示分类，在 Shared system 或显示层读取 component 即可。
9. 若能力会移动自己、移动别人、修改占用、形成 body、产生 pending continuation 或参与冲突，必须接入 `Shared/DG.GameCore/Rules` 下的 state-driven rule 主路。
10. 如果 component 影响 snapshot/delta，同步更新 `EntitySnapshot`、协议和相关同步测试。
11. 如果 component 需要 Unity 视觉表现，只改显示层映射，不在 `ClientMapWorld` 里写规则裁决。
12. 补 Unity TestFramework EditMode 测试、Shared/server 验证和必要的 Play Mode 手测说明。

## 数据源边界

`Config/Luban/Defines/gamecore.xml` 是 `ComponentKind` 配置来源。Luban 生成枚举和 Shared runtime 枚举必须保持 name/value 一致。

`Config/Luban/Datas/gamecore/entity_archetype.xlsx` 是正式实体组合入口。`FallbackGameConfigProvider` 只用于测试、编辑器兜底或最小 demo 兜底。`DefaultWorldConfig` 可以保存 demo 常量和测试 helper，但新增正式实体不应该把它当主配置表。

`Config/Luban/Generated/json`、`Client/DG_Client/Assets/StreamingAssets/GameConfig`、`Shared/DG.GameCore/Config/Generated/LubanTables` 都是导出或生成产物。排查漂移时要同时看源文件、导出产物和运行时代码调用，不能只看 generated 是否存在。

`Tools/NetworkProtocol/Outer/OuterMessage.proto` 是客户端到服务端协议的唯一来源。`Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol` 和 `Server/Entity/Generate/NetworkProtocol` 都是导出产物，不允许手写修复。

协议生成统一使用 `Tools/ProtocolExportTool/ExporterSettings.json` 指向的当前工程路径。修改 `.proto` 后，在 `Tools/ProtocolExportTool` 下运行：

```powershell
dotnet .\Fantasy.ProtocolExportTool.dll export --silent
```

如果 generated 协议里已经出现某个 request/response，但 `.proto` 源文件没有对应 message，按数据源漂移处理：先补 `.proto`，再导出，不要继续扩大手写 generated。

## Entity 构建链路

实体组合主链路：

1. `Config/Luban/Defines/gamecore.xml` 定义 `ComponentKind`。
2. `Tools/sync_luban_component_kind.py` 同步到 `Shared/DG.GameCore/Config/ComponentKind.cs`。
3. `LubanGameConfigProvider.TryGetArchetype` 把 Luban row 转成 `EntityArchetype`。
4. `GameWorld.AddEntity(EntitySpawnSpec)` 委托 `EntityBuilder.AddEntity`。
5. `EntityBuilder` 创建 `GameEntity`，复制 archetype tags，遍历 `archetype.Components`。
6. `ComponentApplicationRegistry.Apply` 把 `ComponentKind` 构造成具体 component。
7. `GameWorld.SetComponent` 写入 `ComponentStore<T>`，并处理位置、碰撞、空间索引和 dirty 副作用。

当前 `GameEntity` 仍保存 `EntityId`、`ConfigId`、`ArchetypeId`、`EntityTarget` 和字符串 tags。这是当前自研 ECS-like 结构的边界，不要把业务规则写回 `GameEntity` 方法里。

`ComponentApplicationRegistry` 仍是代码注册表。这是当前第一版明确接受的硬编码集中点；新增 component 时可以改这里，但不要把规则裁决写进这里。它只负责 component 构造和初始参数应用。

## Component 与 System 边界

Component 是纯数据。它表达能力、状态或参数，不直接执行逻辑。

System 读取 component 和 world state，产生 `WorldAction`、`BehaviorIntent`、commit proposal、move result 或 world state 变化。

`EntityBuilder` 只负责按 archetype 创建 entity、复制 tags、遍历 component kind。具体 component 的构造放在 `ComponentApplicationRegistry`。

如果 component 参数只来自通用实例数据，优先使用 `EntitySpawnSpec` 里的 `position`、`direction`、`playerId`、`autoMoveIntervalTicks` 等字段。

如果字段只服务一个组件，不要直接塞进 `EntityArchetype`。先判断是否需要：

- 组件专用配置表
- component parameter 表
- 后续统一参数 schema

`PortConnectorComponent` 当前就是专用配置表路线的例子。

## Rules 目录与 State-Driven Rule 边界

Shared 规则层当前按 world rule 语义放在 `Shared/DG.GameCore/Rules`：

- `Rules/Actions/WorldActions.cs`：tick/action 输入队列。
- `Rules/Intents/BehaviorIntent.cs`：intent kind 与默认策略定义。
- `Rules/Arbitration/IntentArbiter.cs`：body、tag、priority、cancel policy 仲裁。
- `Rules/Planning/RulePlanning.cs`：intent 到 plan 的规划。
- `Rules/Commit/ConflictResolver.cs`：同 tick 冲突与原子提交。
- `Rules/Commit/CommitRules.cs`：commit proposal 与状态修改。
- `Rules/Pending/PendingRuleStates.cs`：pending continuation 状态。
- `Rules/Execution/StateDrivenRules.cs`：规则执行入口。
- `Rules/Connectivity/PortConnectionSystem.cs`：端口连接体解析。
- `Rules/Primitives/Direction.cs` 和 `Rules/Commands/Movement.cs`：规则层基础类型与 legacy 兼容命令类型。

`Shared/DG.GameCore/Movement` 不再是规则管线主入口。发现新代码继续把 action、intent、planning、arbitration、pending、commit 放回 Movement 目录时，优先按目录回归处理。

新增 component-system 时，先判断它是简单能力还是规则入口能力。

简单能力只新增 component 和读取它的 system，例如 marker、显示分类、静态阻挡、纯状态、参数。

规则入口能力必须进入行为裁决层，例如：

- 移动自己
- 移动别人
- 把多个 entity 当成一个 body
- 改变占用
- 产生 pending continuation
- 传送带或机关推力
- 自动移动
- 技能推力
- 抓取绑定
- 旋转
- 传送

当前权威规则主路：

```text
WorldAction
-> BehaviorIntent
-> IntentArbiter
-> RulePlanner
-> ConflictResolver
-> Commit
```

规则入口 system 只应该产生 `WorldAction` 或 `BehaviorIntent`，再让 `IntentArbiter` 按 body、tag、priority、cancel policy 仲裁，让 `RulePlanner` 解析成 `MovePlan`，最后由 `ConflictResolver` 原子裁决。不要在新 system、Unity 客户端或 `ClientMapWorld` 中直接改 `PositionComponent` 绕过这条路。

新增规则入口时，需要明确：

- source tag
- ability tag
- required tags
- blocked tags
- cancel policy
- 被更高优先级行为打断时的 reason

这些默认策略集中在 `BehaviorIntentDefinitions`。未知 `BehaviorIntentKind` 必须显式失败，不允许静默 fallback 为玩家语义。

Tag 只表达语义分类、能力和状态阻挡，不承载冷却、距离、数值、消耗、动画等规则数据。当前 tag / ability / cancel 只是借鉴 GAS 的仲裁表达，不是完整 GAS。不要在这一层引入冷却、属性、消耗、GameplayCue、客户端预测或完整技能生命周期。

当前第一版已落地 `MovePlan`：单体 body、port-connected body、玩家 push continuation、auto move 和 mechanism push 都应该复用这条路径。Rotate、Teleport、Spawn、Remove 仍是后续 plan 边界，不要在新增玩法里临时复制一套位置提交逻辑。

旧 `MovementResolveSystem` / `AutoMoveSystem` / `PushOnEnterSystem` 不应再作为新扩展点。若发现它们出现在服务端权威 provider、tick runner、ClientWorld 默认 runtime 或新测试主路中，按回归处理。客户端默认 server-authoritative runtime 不应通过 legacy movement resolver 本地裁决移动。

## 协议与运行时边界

Debug RPC、move RPC、snapshot/delta notify 都必须先定义在 `Tools/NetworkProtocol/Outer/OuterMessage.proto`。

新增或修复协议时：

1. 修改 `.proto`。
2. 运行 `Tools/ProtocolExportTool` 导出。
3. 确认客户端和服务端生成物都更新。
4. 服务端 Handler 只依赖生成类型，不复制协议字段定义。
5. Unity 客户端调用生成 helper，不手写 Session 扩展。

`DebugSpawn`、`DebugMove`、`DebugRemove` 当前走 `AuthoritativeInputQueue`。新增 debug action 时先判断它是否影响 tick 语义：

- 影响移动、仲裁、机关、tag 阻挡或 snapshot/delta 一致性时，应进入 tick/action pipeline。
- 仅作为临时 debug state change 时，必须在报告或 spec 中标明即时特例，避免工具面板误以为所有 debug action 都是同一语义。

协议导出、server build、服务端验证不要并行执行，避免 `Server/Entity/obj` 生成物被多个进程同时写入。

## ClientMapWorld 边界

Client world 运行时代码位于 `Client/DG_Client/Assets/Scripts/ClientWorld`。`ClientMapWorld` 是 Shared `GameWorld` 的 Unity 适配层，负责 spawn、snapshot、delta、remove 和 view handle。

新增规则 component-system 时，不在 `ClientMapWorld` 里重新裁决移动、阻挡、推动、自动行为或端口连接体。

显示层可以按 config id、tag 或 Shared component 状态选择 prefab、颜色、ghost 和调试绘制。显示层不决定权威坐标。

客户端提交移动应走 network submitter 到服务端 tick，再通过 response / `WorldDelta` 应用最终状态。默认 server-authoritative runtime 不应提前移动权威镜像；如果后续重新引入离线 helper，必须显式命名为 non-authoritative/offline，并确保它不进入默认 ClientWorld runtime。

## Sandbox 验证

新增或调整 Luban entity/component/tag 后，优先在 `ClientWorldSandboxLab` 中验证：

1. 打开或重新生成 Demo 场景。
2. 从 Luban entity palette 查找新增 config id。
3. 在本地 Shared 模式运行 JSON 用例，确认 parser、tag 和规则预期通过。
4. 在服务端权威模式用 Debug RPC 生成、拖拽、加/移 tag。
5. 确认变化通过服务端 response 和 `WorldDelta` 回到客户端。

测试用例只能引用 Luban config id、坐标、方向、alias、`WorldTag` 和预期结果，不保存 component 组合，不把 JSON 当成第二套实体配置。

## 验证命令

OpenSpec 单个 change：

```powershell
openspec validate <change-id> --strict --no-interactive
```

OpenSpec 全量：

```powershell
openspec validate --all --strict --no-interactive
```

Shared GameCore：

```powershell
dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore
```

协议导出：

```powershell
Push-Location Tools/ProtocolExportTool
dotnet .\Fantasy.ProtocolExportTool.dll export --silent
Pop-Location
```

服务端验证：

```powershell
dotnet build Server/Hotfix/Hotfix.csproj --no-restore -v minimal
dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore
```

Unity EditMode：

1. 打开 `Client/DG_Client`。
2. 打开 Test Runner。
3. 运行 EditMode tests。
4. 重点确认 `ComponentSystemWorkflowTests`、`ClientMapWorldCompositionTests`、`PushOnEnterTileTests`、`IntentArbitrationTests`、`BehaviorArbitrationTests`。

手动端到端：

1. 运行 Luban 导出。
2. 打开 Demo 或 Sandbox 场景。
3. 确认 player、ball、blocker、pushable、port connector、conveyor 仍能生成。
4. 确认新增 component 的实体能通过 Luban 配置生成。
5. 确认客户端显示变化来自服务端 response、snapshot/delta 或 Shared mirror state。

## 后续维护

如果这个流程继续变复杂，优先扩展本 reference，而不是把大量细节塞回 `SKILL.md`。

如果重复校验操作越来越多，再考虑给 skill 增加 `scripts/`，例如 ComponentKind 漂移检查、Luban/StreamingAssets 同步检查、legacy server-authoritative 引用检查。
