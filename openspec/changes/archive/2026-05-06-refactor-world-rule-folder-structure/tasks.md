## 1. 目录审计
- [x] 1.1 列出 `Shared/DG.GameCore/Movement` 下所有文件和引用。
- [x] 1.2 列出 `Client/DG_Client/Assets/Scripts/Map` 下所有文件、`.meta` 和场景/prefab 引用。
- [x] 1.3 确认 `Movement/Legacy` 没有运行时文件和 C# 引用。
- [x] 1.4 确认本次迁移不改变 `WorldActionQueue -> StateDrivenRuleExecutionSystem -> IntentArbiter -> RulePlanner -> ConflictResolver` 行为链路。

## 2. Shared Rules 目录迁移
- [x] 2.1 新建 `Shared/DG.GameCore/Rules` 目录结构。
- [x] 2.2 将 `Movement/Actions` 迁移到 `Rules/Actions`。
- [x] 2.3 将 `Movement/Commands` 迁移到 `Rules/Commands`，并确认 `MoveCommand` 仅作为历史命令类型保留且不在权威主路径使用。
- [x] 2.4 将 `Movement/Primitives` 迁移到 `Rules/Primitives`。
- [x] 2.5 将 `Movement/Pending` 迁移到 `Rules/Pending`。
- [x] 2.6 将 `Movement/Connectivity` 迁移到 `Rules/Connectivity`。
- [x] 2.7 将 `Movement/Rules` 迁移到 `Rules/Execution`。
- [x] 2.8 将 `Movement/Commit` 迁移到 `Rules/Commit`。
- [x] 2.9 删除空的 `Movement/Legacy` 和空目录 `.meta`。

## 3. 规则文件拆薄
- [x] 3.1 从 `BehaviorArbitration.cs` 拆出 `BehaviorIntent` 和 `BehaviorIntentDefinitions`。
- [x] 3.2 拆出 `IntentArbiter` 与 arbitration result 类型。
- [x] 3.3 拆出 `BodyResolver`、`OccupancyResolver` 和 `RulePlanner`。
- [x] 3.4 拆出 `ConflictResolver` 到 commit 边界。
- [x] 3.5 确认拆分后 public API 名称和行为不变。

## 4. Unity ClientWorld 目录迁移
- [x] 4.1 新建 `Client/DG_Client/Assets/Scripts/ClientWorld` 目录结构。
- [x] 4.2 迁移 `Map/Bootstrap` 到 `ClientWorld/Bootstrap`。
- [x] 4.3 迁移 `Map/Runtime` 到 `ClientWorld/Runtime`。
- [x] 4.4 迁移 `Map/Networking` 到 `ClientWorld/Networking`。
- [x] 4.5 迁移 `Map/View` 到 `ClientWorld/View`。
- [x] 4.6 迁移 `Map/Debug` 到 `ClientWorld/Debug`。
- [x] 4.7 迁移 `Map/Interaction` 和 `Map/Spatial` 到 `ClientWorld` 下对应目录。
- [x] 4.8 审计 `DG.Map` namespace 是否同步改名；本次只改目录并保留 namespace，避免 Unity 序列化风险。

## 5. 引用与测试整理
- [x] 5.1 更新代码引用、Unity editor builder 路径和测试路径。
- [x] 5.2 确认 Unity 生成项目文件包含新路径。
- [x] 5.3 运行 `openspec validate refactor-world-rule-folder-structure --strict --no-interactive`。
- [x] 5.4 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 5.5 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 5.6 在 Unity Test Runner 手动运行 EditMode：`ClientMapWorldCompositionTests`、`IntentArbitrationTests`、`BehaviorArbitrationTests`、`ComponentSystemWorkflowTests`、`PushOnEnterTileTests`。

## 6. 手动验证
- [x] 6.1 打开 demo 场景确认 `ClientWorldRunner`、网络提交器和显示脚本未丢失引用。
- [x] 6.2 启动服务端和客户端执行 JoinWorld。
- [x] 6.3 移动玩家，确认坐标变化来自服务端 response 或 WorldDelta。
- [x] 6.4 验证传送带、连接体冲突、tag 阻断仍走 Shared Rules 管线。
