# DG 项目当前整理报告

评估日期：2026-05-05  
评估范围：`D:\Unity_Project_1\DG` 当前工作区  
评估方式：读取项目规则、OpenSpec、项目级 skill、核心代码、Unity 编辑器工具入口、沙盒测试代码、协议源与生成物、当前工作区状态；不执行 Unity Player build。

## 1. 当前结论

DG 现在已经形成了服务端权威 2D 格子沙盒的基础链路：

- `Shared/DG.GameCore` 是纯 C# 世界核心，负责实体、组件、空间索引、移动规则、行为仲裁、dirty、snapshot 和 delta。
- `Server/Hotfix/AuthoritativeMove` 是 Fantasy 服务端外壳，负责 join、move RPC、debug RPC、tick runner、input queue、WorldDelta/snapshot 同步。
- `Client/DG_Client` 是 Unity 客户端外壳，负责本地镜像 `ClientMapWorld`、Fantasy Session 调用、服务端 snapshot/delta 应用和简单可视化。
- `Config/Luban` 已经成为实体组合数据源，`Run.ps1` 会生成 Shared Luban 表代码与 Unity StreamingAssets JSON。
- 旧的运行时 UI/debug 层已经移除，当前不再保留 `ClientWorldDebugEditor`、`ClientWorldSandboxLab`、`ClientNetworkDebugOverlay` 这些运行时 UI 入口。

目前最应该收口的是三件事：

1. 把编辑器工具和运行时功能边界整理清楚。
2. 修复协议源与生成物漂移，尤其是 `DebugSetEntityTag`。
3. 等你手拼统一工具面板后，把面板绑定到现有底层 API，而不是恢复旧 UI。

## 2. 已验证状态

### 2.1 轻量验证

此前本工作区已经跑过这些验证：

```powershell
openspec validate --all --strict --no-interactive
dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore -v minimal
dotnet build Server/Hotfix/Hotfix.csproj --no-restore -v minimal
dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore
```

结果：

- OpenSpec：14 项通过，0 失败。
- Shared：`DG.GameCore` 编译通过。
- Server Hotfix：`DG.GameCore`、`Entity`、`Hotfix` 编译通过。
- 服务端验证项目：输出 `Authoritative move verification passed.`。

本轮没有执行 Unity Player build，符合项目约束。

### 2.2 Unity 编辑器验证

旧 UI/debug 层移除后，已经通过 Unity MCP 做过当前检查：

- Unity Console：没有项目编译错误；只看到 MCP WebSocket 相关 warning。
- Scene Validate：`ClientWorldRunnerDemo` 没有 missing script。
- Unity EditMode：`47/47 passed`。
- 当前 scene 仍包含 `ClientMoveNetworkSubmitter` 与 `FantasyRuntime`。
- 当前 scene 不包含 `ClientWorldDebugEditor`、`ClientWorldSandboxLab`、`ClientNetworkDebugOverlay`。

还没有完成的验证：

- 单客户端完整 Play Mode 手动验证。
- 双客户端同步验证。
- 新统一工具面板端到端验证。

## 3. 当前编辑器功能

### 3.1 可保留入口

当前真正存在的编辑器入口只有两个：

- `DG/Map/Create Client ClientMapWorld Runner Demo Scene`
  - 文件：`Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Editor/ClientWorldDemoSceneBuilder.cs`
  - 作用：生成 `Assets/Scenes/ClientWorldRunnerDemo.unity`。
  - 当前会创建 `ClientWorld`、`WorldBootstrap`、`ClientWorldRunner`、`ClientMoveNetworkSubmitter`、`ClientWorldDemo`、`ClientWorldVisuals`、`FantasyRuntime`、`Main Camera`。
  - 不再创建旧 runtime UI/debug 组件。

- `DG/Map/Chunk Debug`
  - 文件：`Client/DG_Client/Assets/Scripts/Editor/MapChunkDebugWindow.cs`
  - 作用：检查世界坐标、chunk 坐标、本地坐标转换，并在 SceneView 里画 chunk/cell。
  - 这是 EditorWindow，不是运行时 UI，可以先保留。

### 3.2 已删除的运行时 UI/debug 层

这些旧入口已经从代码和 scene 中移除：

- `ClientWorldDebugEditor`
- `ClientWorldSandboxLab`
- `ClientNetworkDebugOverlay`

相关测试里的旧 UI 覆盖也已经移除或改名：

- `DebugWorldEditorTests` 已不再作为旧 UI 测试存在。
- 当前保留的是网络运行时相关测试 `ClientMoveNetworkRuntimeTests`。
- `SandboxScenarioTests` 保留 parser、palette、local runner、storage 等底层测试，不再测运行时 uGUI。

结论：现在不是“把以前功能全删了”，而是只删了旧 runtime UI/debug 壳。底层网络、世界镜像、沙盒用例解析和本地 runner 仍在。

## 4. 当前代码分层

### 4.1 Shared GameCore

核心文件：

- `Shared/DG.GameCore/World/GameWorld.cs`
- `Shared/DG.GameCore/Components/Components.cs`
- `Shared/DG.GameCore/Config/EntityBuilder.cs`
- `Shared/DG.GameCore/Config/ComponentApplicationRegistry.cs`
- `Shared/DG.GameCore/Movement/Rules/StateDrivenRules.cs`
- `Shared/DG.GameCore/Movement/Arbitration/BehaviorArbitration.cs`
- `Shared/DG.GameCore/Movement/Connectivity/PortConnectionSystem.cs`
- `Shared/DG.GameCore/Testing/SandboxScenario.cs`

当前职责：

- `GameWorld` 保存实体、组件、空间索引、dirty、snapshot 和 delta。
- `EntityBuilder` 通过 Luban/Fallback provider 和 `ComponentApplicationRegistry` 创建实体组件。
- `StateDrivenRules` 承担 action queue、pending、commit、规则执行。
- `SandboxScenario` 已经包含 JSON document、parser、validator、palette、storage、本地 runner。

主要风险：

- `StateDrivenRules.cs` 单文件仍然偏大，后续新增规则前最好先拆文件。
- `GameWorld` 同时承担存储、索引、dirty、snapshot，短期可接受，中期需要拆 query/snapshot/delta builder。
- `WorldTag` 仍是 C# enum，`EntityArchetype.tags` 是字符串列表，边界需要继续保持清晰。

### 4.2 Server / Fantasy

核心文件：

- `Server/Hotfix/AuthoritativeMove/World/AuthoritativeMoveWorldProvider.cs`
- `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
- `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeInputQueue.cs`
- `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldSyncSystem.cs`
- `Server/Hotfix/AuthoritativeMove/Handlers/C2G_MoveRequestHandler.cs`
- `Server/Hotfix/AuthoritativeMove/Handlers/C2G_DebugSpawnEntityRequestHandler.cs`
- `Server/Hotfix/AuthoritativeMove/Handlers/C2G_DebugMoveEntityRequestHandler.cs`
- `Server/Hotfix/AuthoritativeMove/Handlers/C2G_DebugRemoveEntityRequestHandler.cs`
- `Server/Hotfix/AuthoritativeMove/Handlers/C2G_DebugSetEntityTagRequestHandler.cs`

当前职责：

- Handler 接收 RPC，尽量把行为交给 tick/input/world 服务处理。
- `AuthoritativeInputQueue` 统一承接玩家移动和 debug action。
- `AuthoritativeWorldTickRunner` 每 tick 消费输入、执行规则、flush delta。
- `AuthoritativeWorldSyncSystem` 发送 snapshot/delta。

主要风险：

- `DebugSetEntityTag` 当前仍是特殊链路，需要确认它是否也必须进入 tick/action pipeline。
- `DebugWorldEditService` 仍保留一些旧直接修改世界的方法，后续统一工具面板不要直接绕过 tick 语义调用它们。
- 当前同步还是全 observer 广播，不是 AOI/可见性过滤。

### 4.3 Unity Client

核心文件：

- `Client/DG_Client/Assets/Scripts/Map/World/ClientMapWorld.cs`
- `Client/DG_Client/Assets/Scripts/Map/Networking/Runtime/ClientMoveNetworkRuntime.cs`
- `Client/DG_Client/Assets/Scripts/Map/Networking/Runtime/ClientMoveNetworkSubmitter.cs`
- `Client/DG_Client/Assets/Scripts/Map/Networking/Handlers/G2C_WorldDeltaNotifyHandler.cs`
- `Client/DG_Client/Assets/Scripts/Map/Networking/Handlers/G2C_WorldSnapshotNotifyHandler.cs`
- `Client/DG_Client/Assets/Scripts/Map/Networking/Handlers/G2C_EntityMovedNotifyHandler.cs`
- `Client/DG_Client/Assets/Scripts/Map/View/ClientWorldVisuals.cs`

当前职责：

- `ClientMapWorld` 是 Shared `GameWorld` 的 Unity 镜像适配层。
- `ClientMoveNetworkSubmitter` 负责 JoinWorld、RegisterObserver、Move、DebugSpawn、DebugMove、DebugRemove、DebugSetTag。
- 网络 notify handler 把服务端 snapshot/delta 应用到本地镜像。
- `ClientWorldVisuals` 做简单运行时可视化。

主要风险：

- `ClientMoveNetworkSubmitter` 现在可以直接提供统一面板要调用的方法，但面板必须走它，不要直接改 `ClientMapWorld`。
- `ClientWorldVisuals` 仍是临时视觉，不是正式 prefab catalog。
- 重连、断线、observer 重新注册还没有完整 UI 状态机。

## 5. 沙盒测试台当前状态

OpenSpec 任务仍显示：

| Change | 状态 |
|---|---:|
| `add-luban-driven-sandbox-test-lab` | 0/117 tasks |

但代码现实不是 0 实现。当前已经存在：

- JSON 测试用例 document。
- parser。
- validator。
- Luban entity palette。
- storage save/load。
- 本地 Shared runner。
- Unity EditMode 覆盖 parser、未知 config id、未知 tag、禁止手写 archetype、palette、local runner、storage。

当前缺失的是：

- 正式统一工具面板。
- 面板到 `ClientMoveNetworkSubmitter` 的绑定。
- 权威模式下的保存/加载/执行入口。
- 对应 OpenSpec `tasks.md` 的真实勾选更新。

建议：你手拼面板时，只需要先提供控件引用和按钮结构。我后续可以很快把它接到现有底层：

- entity palette 列表接 `SandboxEntityPalette.FromProvider(...)`。
- spawn/move/remove/tag 接 `ClientMoveNetworkSubmitter`。
- 本地用例运行接 `LocalSandboxScenarioRunner`。
- 保存/加载接 `SandboxScenarioStorage`。
- 结果显示接 `SandboxScenarioRunResult` 和 RPC response。

## 6. 当前 P0/P1 风险

### P0：`DebugSetEntityTag` 协议源漂移

当前事实：

- `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol/OuterMessage.cs` 里有 `C2G_DebugSetEntityTagRequest`。
- `Server/Entity/Generate/NetworkProtocol/OuterMessage.cs` 里有 `C2G_DebugSetEntityTagRequest`。
- `ClientMoveNetworkSubmitter` 已经调用 `session.C2G_DebugSetEntityTagRequest(...)`。
- `Server/Hotfix/AuthoritativeMove/Handlers/C2G_DebugSetEntityTagRequestHandler.cs` 已经存在。
- 但 `Tools/NetworkProtocol/Outer/OuterMessage.proto` 只包含 DebugSpawn、DebugMove、DebugRemove，没有 DebugSetEntityTag。

影响：

- 重新导出协议时，DebugSetTag 可能消失或 opcode 漂移。
- 新工具面板接 tag 功能时，会踩到协议源不可信的问题。

建议先修：

1. 在 `.proto` 增加 `C2G_DebugSetEntityTagRequest` 和 `G2C_DebugSetEntityTagResponse`。
2. 重新跑协议导出。
3. 不手写 generated C#。
4. 跑 Server Hotfix 编译和服务端验证项目。
5. 跑 Unity EditMode。

### P0：OpenSpec active change 太多且任务状态不可信

当前 `openspec list` 显示 9 个 active change：

| Change | 状态 |
|---|---:|
| `add-luban-driven-sandbox-test-lab` | 0/117 tasks |
| `add-gameplay-tag-intent-arbitration` | 58/63 tasks |
| `refactor-behavior-arbitration-layer` | 69/76 tasks |
| `add-port-connected-push-ability` | 33/37 tasks |
| `refactor-component-system-workflow` | Complete |
| `refactor-unified-tick-cost-pipeline` | 87/105 tasks |
| `refactor-state-driven-rule-execution` | 0/68 tasks |
| `add-state-driven-push-rules` | 41/45 tasks |
| `refactor-authoritative-tick-flush` | 40/44 tasks |

建议：

1. 先不要再开新 gameplay change。
2. 先修协议漂移。
3. 再审计并归档 `refactor-component-system-workflow`。
4. 对 `refactor-state-driven-rule-execution` 做任务状态复核，因为代码现实和 0/68 明显不一致。
5. `add-luban-driven-sandbox-test-lab` 等统一面板出来后，再按真实完成情况更新任务。

### P1：旧运行时 UI 问题已经通过删除解决，但工具入口空出来了

原问题：

- 旧 `ClientWorldDebugEditor` 是 IMGUI + 硬编码槽位。
- 旧 `ClientWorldSandboxLab` / overlay 的 UI 结构不适合继续扩。
- 它们会让新 Luban entity、保存/加载、用例回放越来越难维护。

当前判断：

- 这些旧 runtime UI 类已经从代码、scene 和测试里移除。
- 这个问题不是“已完整解决”，而是“旧错误方向已清掉”。
- 现在缺的是统一工具面板，以及面板与底层 API 的稳定绑定层。

下一步：

1. 不恢复旧 runtime UI。
2. 保留 `ClientMoveNetworkSubmitter`、`ClientMapWorld`、`SandboxScenario` 等底层能力。
3. 等手拼统一面板后，只接功能，不重新生成 UI 结构。
4. 为新面板补 Unity EditMode：控件绑定、非法输入、合法调用、结果显示。

### P1：`DebugSetEntityTag` 是否进入 tick/action pipeline 尚未定

原问题：

- DebugSpawn、DebugMove、DebugRemove 已经走 `AuthoritativeInputQueue`。
- DebugSetTag 仍是独立 Handler 直接改 tag。

当前判断：

- 如果 tag 会影响移动、机制推动、仲裁结果，它的 tick 边界就必须明确。
- 如果它只是即时调试特例，也要在 spec 中写清楚，避免统一工具面板误以为所有 debug action 都同语义。

下一步：

1. 先修 `.proto` 源漂移。
2. 再决定 SetTag 是即时 debug state change，还是 `AuthoritativeInputQueue` 中的 action。
3. 如果进入 tick queue，补 `DebugSetTag` action、tick 执行、delta flush、测试。
4. 如果保留即时特例，在 OpenSpec 和报告里标明限制。

### P1：`StateDrivenRules.cs` 单文件过大

原问题：

- action queue、pending state、commit proposal、commit resolver、rule execution、push state 都集中在 `StateDrivenRules.cs`。

当前判断：

- 这不是当前阻断项，但会阻碍后续新增 Rotate、Teleport、Spawn、Remove、Door、Portal 等规则。
- 现在不适合顺手重构，因为 active change 太多，容易扩大范围。

下一步：

1. 先不改行为。
2. 等协议和 OpenSpec 状态收口后，单独拆文件。
3. 拆分顺序：`WorldActionQueue`、`PendingRuleStateStore`、`CommitProposal`、`CommitResolver`、`StateDrivenRuleExecutionSystem`、`PushPropagationState`。
4. 每拆一步跑服务端验证和 Unity EditMode。

### P1：同步仍是全 observer 广播

原问题：

- `AuthoritativeWorldSyncSystem.BroadcastDelta` 对 observers 广播同一份 delta。
- `MoveObserverRegistry` 还不是真正 AOI/VisibilityTracker。

当前判断：

- 当前 demo 阶段可用。
- 大地图、多玩家、可见性同步阶段一定要拆。
- 不应该现在混进统一工具面板任务里。

下一步：

1. 当前只保留全量 observer delta，先把 demo 测通。
2. 后续单独开 `VisibilityTracker` / per-session visible set 相关 change。
3. 最小切片：entered、changed、left 三类同步。
4. 手动测试至少三客户端：可见、离开视野、重新进入视野。

### P1：沙盒测试台 OpenSpec 状态与代码现实不一致

原问题：

- `add-luban-driven-sandbox-test-lab` 显示 0/117。
- 原任务仍提到 `ClientWorldDebugEditor` 现状确认。

当前判断：

- 底层已经有 `SandboxScenario`、parser、validator、palette、storage、local runner 和 Unity EditMode 测试。
- runtime UI 被删后，OpenSpec 的前几项需要按新现实调整。
- 不能把现有代码直接算作 change 完成，必须逐条对照 `tasks.md` 勾选。

下一步：

1. 先不勾选 tasks。
2. 等统一面板结构确定后，重审 `tasks.md`。
3. 把“保留旧 `ClientWorldDebugEditor`”相关任务改成“确认旧 runtime UI 已移除，统一面板接底层 API”。
4. 逐条补证据后再勾选。

### P2：Unity 视觉仍是临时形态

原问题：

- `ClientWorldVisuals` 运行时直接生成简单 Sprite/LineRenderer。
- 没有正式 prefab catalog / view registry。

当前判断：

- 这适合当前验证，不适合长期表现层。
- 统一工具面板不应该顺手承担正式表现系统。

下一步：

1. 统一面板只做调试和测试入口。
2. `ClientWorldVisuals` 暂时保留。
3. 后续单独做 entity view catalog：config id / component / tag 到 prefab 或 visual rule。

### P2：工程化与路径可迁移性不足

原问题：

- 协议导出配置含本机绝对路径。
- 部分包版本使用宽泛版本。
- Luban、协议、生成物、StreamingAssets 多份产物容易漂移。

当前判断：

- 这不是当前 UI 面板阻断项。
- 但协议源漂移已经说明再生成链路必须收紧。

下一步：

1. 优先修 `DebugSetEntityTag` `.proto`。
2. 再检查协议导出路径是否能相对化。
3. 后续增加 Luban/config/protocol 一键自检。

## 7. 下一步执行计划

### 7.1 立刻做

1. 修 `Tools/NetworkProtocol/Outer/OuterMessage.proto` 的 `DebugSetEntityTag` 缺失。
2. 重新导出协议，不手写 generated C#。
3. 跑服务端轻量验证。
4. 跑 Unity EditMode。

### 7.2 然后做

1. 整理 `ClientMoveNetworkSubmitter` 对外 API。
2. 准备统一面板 adapter，让 UI 只关心字段、按钮和结果。
3. 把 `SandboxScenario` 的错误结果整理成 UI 友好文本。
4. 更新 `add-luban-driven-sandbox-test-lab/tasks.md`，但只勾选有证据的项。

### 7.3 等你拼完 UI 后做

1. 绑定 entity palette。
2. 绑定 spawn/move/remove/tag。
3. 绑定保存/加载 JSON。
4. 绑定本地 runner。
5. 绑定权威 RPC 模式。
6. 补 Unity EditMode。
7. 给你手动端到端测试步骤。

## 8. 你拼 UI 时我可以做什么

最适合我先做的不是继续做 UI，而是把后端接口打磨成统一面板很好接的形状：

1. 修 `DebugSetEntityTag` 协议源漂移。
2. 清理 `ClientMoveNetworkSubmitter` 的对外方法，让统一面板只调用一组稳定 API。
3. 给统一面板准备一个 adapter，例如 `SandboxToolController`，只负责桥接按钮、输入框、palette、RPC、本地 runner。
4. 把 `SandboxScenario` 的错误信息整理成适合 UI 显示的短文本。
5. 给面板写 EditMode 测试：按钮绑定、非法输入不发 RPC、合法输入调用 submitter。
6. 补手动测试说明：服务端启动、Unity Play、Join、Spawn、Move、Tag、Remove、双客户端同步。

## 9. 推荐下一步

按顺序做：

1. 修 `DebugSetEntityTag` `.proto` 源缺失。
2. 跑协议导出和轻量验证。
3. 整理 `ClientMoveNetworkSubmitter` 为统一面板 API。
4. 等你手拼面板。
5. 我把面板按钮和字段接到底层功能。
6. 跑 Unity EditMode。
7. 你做单客户端和双客户端手动端到端测试。

## 10. 当前手动测试路线

统一面板完成前，当前最小手测路线是：

1. 启动服务端。
2. 打开 Unity `Client/DG_Client`。
3. 用菜单 `DG/Map/Create Client ClientMapWorld Runner Demo Scene` 重新生成 demo scene。
4. 进入 Play Mode。
5. Console 期望看到 Fantasy 连接成功、heartbeat started、JoinWorld 成功、WorldSnapshot/WorldDelta 应用。
6. 移动玩家，观察服务端授权坐标。
7. 双客户端验证时，A 移动后 B 应看到 A 的位置变化。

不能把 Shared build、Server build、EditMode tests 直接说成端到端完成。端到端必须你在 Play Mode 和双客户端里确认。

## 11. 自检

这份报告没有声称 Unity Player build 通过，因为项目规则要求不要尝试 build Unity。  
这份报告没有声称双客户端已经验证，因为当前没有重新手动启动两个客户端。  
这份报告没有把 `add-luban-driven-sandbox-test-lab` 的 tasks 勾选当成真实完成，因为 `tasks.md` 仍是 0/117。  
这份报告已经把旧 runtime UI 删除后的现状写清楚：底层功能保留，旧 UI 壳不保留。
