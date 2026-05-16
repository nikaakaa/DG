# Source Layout

## 核心原则
- Shared GameCore 只放纯 C# 领域、行为运行时、世界状态、配置适配和测试辅助，不引用 Unity 或 Fantasy。
- Server Hotfix 只放 Fantasy 权威运行外壳，Handler 不写规则，规则只通过 Shared GameCore 入口执行。
- Unity ClientWorld 只放客户端镜像、展示、输入和调试工具，默认 Play Mode 不做权威规则裁决。
- 新增文件按“这个类改变什么语义”放置，不按“谁调用它”放置。
- 一个文件优先只表达一个主要类型或一组强绑定的小值对象；超过 300 行或包含多个独立职责时继续拆。

## Shared GameCore 目录
- `Shared/DG.GameCore/ActionRuntime/Specs`: action id、spec、policy model、registry。
- `Shared/DG.GameCore/ActionRuntime/Requests`: 外部输入转 action request。
- `Shared/DG.GameCore/ActionRuntime/Queue`: world action queue、deferred action、显式输出策略。
- `Shared/DG.GameCore/ActionRuntime/Lifecycle`: action unit 生命周期状态和迁移记录。
- `Shared/DG.GameCore/ActionRuntime/Execution`: tick 级行为编排。
- `Shared/DG.GameCore/ActionRuntime/Strategies/Contracts`: `IActionStrategy`、strategy context、attribute。
- `Shared/DG.GameCore/ActionRuntime/Strategies/Generation`: strategy registration 生成器。
- `Shared/DG.GameCore/ActionRuntime/Strategies/Implementations`: 具体 action strategy。
- `Shared/DG.GameCore/ActionRuntime/Targeting/Contracts`: selector contract、target data、targeting result。
- `Shared/DG.GameCore/ActionRuntime/Targeting/Selectors`: selector implementation。
- `Shared/DG.GameCore/ActionRuntime/Targeting/Filters`: target filter 和 component fact query。
- `Shared/DG.GameCore/ActionRuntime/Gating`: tag/component gate。
- `Shared/DG.GameCore/ActionRuntime/Subjects`: action subject 选择。
- `Shared/DG.GameCore/ActionRuntime/Claims`: claim 数据、claim 构造、claim arbitration、push vector composition。
- `Shared/DG.GameCore/ActionRuntime/Blocking/Contacts`: blocking contact 收集。
- `Shared/DG.GameCore/ActionRuntime/Blocking/Policies`: blocked policy 匹配。
- `Shared/DG.GameCore/ActionRuntime/Blocking/Outcomes`: derive、bounce、reject、noop 等业务 outcome。
- `Shared/DG.GameCore/ActionRuntime/Planning`: plan 构造、body capability、port graph、connected body 视图。
- `Shared/DG.GameCore/ActionRuntime/Commit`: commit proposal/result/context/registry/resolver。
- `Shared/DG.GameCore/ActionRuntime/Commit/Handlers`: 具体 world mutation handler。
- `Shared/DG.GameCore/ActionRuntime/Generated`: action runtime 自动生成注册代码。
- `Shared/DG.GameCore/Domain/Components`: final component 数据、tag 数据、component 值对象。
- `Shared/DG.GameCore/Domain/Entities`: `GameEntity` 和实体身份模型。
- `Shared/DG.GameCore/Domain/ValueObjects`: direction 等稳定值对象。
- `Shared/DG.GameCore/Configuration`: 运行时配置模型、provider、fallback provider。
- `Shared/DG.GameCore/Configuration/Luban`: Luban adapter、loader、runtime compat。
- `Shared/DG.GameCore/Configuration/Generated/LubanTables`: Luban 生成代码，不能手写修改。
- `Shared/DG.GameCore/World`: `GameWorld` 公开边界。
- `Shared/DG.GameCore/World/Storage`: storage adapter、query cache、entity/component registry。
- `Shared/DG.GameCore/World/Spatial`: 坐标、chunk、cell、空间索引和 dirty tracking。
- `Shared/DG.GameCore/World/Snapshots`: snapshot/delta 合同和 component projector/applier。
- `Shared/DG.GameCore/RuntimeEffects/Specs`: runtime effect id/spec/enums。
- `Shared/DG.GameCore/RuntimeEffects/Store`: runtime effect instance/store。
- `Shared/DG.GameCore/RuntimeEffects/Settlement`: effect application、lifecycle、component result resolver。
- `Shared/DG.GameCore/Testing`: 测试和调试复现辅助，不作为正式运行时配置来源。

## 新增 Shared 行为放置规则
- 新增普通行为策略：放 `ActionRuntime/Strategies/Implementations`，实现 `IActionStrategy`，通过 strategy registry 注册；不得在 `StateDrivenRuleExecutionSystem` 或 `GameWorld` 按 action 名分支。
- 新增 action policy 字段或枚举：放 `ActionRuntime/Specs`；Luban 映射放 `Configuration/Luban`。
- 新增 targeting selector：放 `ActionRuntime/Targeting/Selectors`；只通过 `TargetSelectorRegistry` 暴露。
- 新增 target filter 或 component fact query：放 `ActionRuntime/Targeting/Filters`。
- 新增 blocked policy 匹配：放 `ActionRuntime/Blocking/Policies`。
- 新增 blocked outcome 业务分支：放 `ActionRuntime/Blocking/Outcomes`；不得写进具体 strategy。
- 新增 claim 数据或构造规则：放 `ActionRuntime/Claims`；不得写进 commit。
- 新增 connected body / port 规则：放 `ActionRuntime/Planning`，按“图视图”和“能力解析”区分。
- 新增 commit 规则：放 `ActionRuntime/Commit/Handlers`；不得在 Handler、client mirror 或 debug UI 里提交最终坐标。
- 新增 component：放 `Domain/Components`，如果是一个独立语义就建独立文件；同时更新 `ComponentKind`、`ComponentApplicationRegistry`、archetype/provider 映射和测试。
- 新增 config provider 或正式配置映射：放 `Configuration` 或 `Configuration/Luban`；生成代码只进 `Configuration/Generated/LubanTables`。
- 新增 world 查询/storage 内部结构：放 `World/Storage` 或 `World/Spatial`，保持 `GameWorld` 是外部调用入口。
- 新增 runtime effect 类型：按 id/spec/application/instance/store/lifecycle 分别放到 `RuntimeEffects` 子目录，不要填回一个大文件。

## Server Hotfix 目录
- `Server/Hotfix/AuthoritativeMove/Handlers`: Fantasy message entrypoints。
- `Server/Hotfix/AuthoritativeMove/Application`: 权威应用服务和 session/entity 管理。
- `Server/Hotfix/AuthoritativeMove/Runtime`: 输入队列和 tick runtime。
- `Server/Hotfix/AuthoritativeMove/Sync`: observer、snapshot/delta 和 broadcast。
- `Server/Hotfix/AuthoritativeMove/Debugging`: 权威调试编辑服务。
- `Server/Hotfix/AuthoritativeMove/WorldBootstrap`: 服务端 world provider 和配置路径。
- `Server/Hotfix/AuthoritativeMove/ProtocolMapping`: 协议与 DG 数据转换。

## 新增 Server 类放置规则
- 新增 `C2G_*` / `G2C_*` Handler：放 `Handlers`，只做校验、转换、调用应用服务、reply。
- 新增服务端 tick 步骤：放 `Runtime`，通过 Shared GameCore API 处理规则。
- 新增同步广播或 observer 逻辑：放 `Sync`。
- 新增调试编辑执行：放 `Debugging`，仍通过权威 `GameWorld` 和 sync 输出。
- 新增 server world 初始化：放 `WorldBootstrap`。
- 新增协议转换 helper：放 `ProtocolMapping`，不把协议类型传进 Shared GameCore。

## Unity ClientWorld 目录
- `Client/DG_Client/Assets/Scripts/ClientWorld/Bootstrap`: 客户端 world 和配置启动。
- `Client/DG_Client/Assets/Scripts/ClientWorld/Networking`: Fantasy push/request 入口和网络 runtime。
- `Client/DG_Client/Assets/Scripts/ClientWorld/Mirror`: 服务端 snapshot/delta 的本地镜像状态。
- `Client/DG_Client/Assets/Scripts/ClientWorld/Presentation`: GameObject 显示、动画和视觉表现。
- `Client/DG_Client/Assets/Scripts/ClientWorld/Input`: Play Mode 输入演示。
- `Client/DG_Client/Assets/Scripts/ClientWorld/DebugTools`: runtime 调试 UI、结构块和 port 可视化。
- `Client/DG_Client/Assets/Scripts/ClientWorld/EditorTools`: Unity Editor 菜单和窗口。

## 新增 Unity 类放置规则
- 新增 server snapshot/delta 应用：放 `Mirror` 或 `Networking/Runtime`，最终写入 mirror。
- 新增 server push handler：放 `Networking/Handlers`，不写规则裁决。
- 新增视觉、动画、GameObject 生命周期：放 `Presentation`。
- 新增键鼠输入和 demo 操作：放 `Input`，只提交 intent。
- 新增 runtime 调试 UI 或结构块工具：放 `DebugTools`。
- 新增 editor menu/window/importer：放 `EditorTools`。
- 新增 EditMode 测试：按被测模块放到 `Assets/Tests/Editor/ClientWorld/<模块名>`。

## 验证要求
- Shared 变更至少运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- Server 变更至少运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- Unity ClientWorld 变更需要 Unity TestFramework EditMode；若 batchmode 被已打开 Editor 阻塞，必须在结果里明确说明。
- 端到端完成需要用户手动运行服务端和两个客户端，验证 Join、移动、调试建造、拖拽、删除和 WorldDelta 同步。
