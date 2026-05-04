## 实现落点

### 协议
- `Tools/NetworkProtocol/Outer/OuterMessage.proto`
- `Server/Entity/Generate/NetworkProtocol/OuterMessage.cs`
- `Server/Entity/Generate/NetworkProtocol/OuterOpcode.cs`
- `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol/OuterMessage.cs`
- `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol/OuterOpcode.cs`
- `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol/NetworkProtocolHelper.cs`

新增协议：
- `C2G_ObserveBouncingDemoRequest`
- `G2C_ObserveBouncingDemoResponse`
- `G2C_DemoEntitySnapshotNotify`
- `G2C_DemoEntityChangedNotify`

### 服务端
- `Server/Hotfix/BouncingDemo/World/BouncingDemoModel.cs`
- `Server/Hotfix/BouncingDemo/World/BouncingDemoWorld.cs`
- `Server/Hotfix/BouncingDemo/World/BouncingDemoWorldProvider.cs`
- `Server/Hotfix/BouncingDemo/System/BouncingDemoTickSystem.cs`
- `Server/Hotfix/BouncingDemo/System/BouncingDemoTickRunner.cs`
- `Server/Hotfix/BouncingDemo/System/BouncingDemoSyncSystem.cs`
- `Server/Hotfix/BouncingDemo/Broadcast/BouncingDemoObserverRegistry.cs`
- `Server/Hotfix/Gate/Handler/C2G_ObserveBouncingDemoRequestHandler.cs`

职责边界：
- `C2G_ObserveBouncingDemoRequestHandler` 只注册 observer、启动 tick runner、回复 observe 结果并触发 snapshot。
- `BouncingDemoTickRunner` 拥有无限 tick loop 和重复启动保护。
- `BouncingDemoTickSystem` 只负责按组件/tag 查询运动体和边界体并计算反弹。
- `BouncingDemoSyncSystem` 只负责 snapshot、dirty flush 和 observer 广播。
- `BouncingDemoWorld` 保存配置实例化 entity、server tick、dirty state 和查询方法。

### Unity 客户端
- `Client/DG_Client/Assets/Scripts/Map/Components/DemoEntityComponent.cs`
- `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/ClientBouncingDemoObserver.cs`
- `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/ClientMoveNetworkRuntime.cs`
- `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/ClientWorldVisuals.cs`
- `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/ClientWorldDemoSceneBuilder.cs`
- `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/G2C_DemoEntitySnapshotNotifyHandler.cs`
- `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/G2C_DemoEntityChangedNotifyHandler.cs`
- `Client/DG_Client/Assets/Scenes/ClientWorldRunnerDemo.unity`

客户端只应用服务端 snapshot/delta，不执行本地反弹权威规则。运动体和边界体通过 `DemoEntityComponent.ConfigId`、`ArchetypeId`、`EntityTarget`、坐标、速度和边界范围进入当前 `World`，再由 `ClientWorldVisuals` 绘制。

### 测试
- `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`
- `Client/DG_Client/Assets/Tests/Editor/Map/ClientWorldRunnerTests.cs`
- `Client/DG_Client/Assets/Tests/Editor/Map/ClientWorldVisualsTests.cs`

## 已执行验证

### 协议导出
```powershell
cd Tools/ProtocolExportTool
dotnet Fantasy.ProtocolExportTool.dll export --silent
```

结果：通过，服务端和客户端生成目录已出现新增消息、opcode 和客户端 helper。

### 服务端构建
```powershell
dotnet build Server\Server.sln -v minimal
```

结果：通过，0 警告，0 错误。

### 服务端规则验证
```powershell
dotnet run --project Server\Tests\AuthoritativeMoveVerification\AuthoritativeMoveVerification.csproj
```

结果：通过，输出 `Authoritative move verification passed.`。

覆盖内容：
- 配置实例化两个 entity。
- 普通移动。
- X 轴反弹。
- Y 轴反弹。
- dirty flush。
- 重复启动保护。
- stop 后不再推进 tick。

### Unity 客户端 C# 构建
```powershell
dotnet build .\Client\DG_Client\Assembly-CSharp.csproj --no-restore -v minimal
dotnet build .\Client\DG_Client\Assembly-CSharp-Editor.csproj --no-restore -v minimal
```

结果：两个工程都通过，0 错误。仍有 Unity 生成 csproj 常见的程序集版本冲突警告。

### OpenSpec 校验
```powershell
openspec validate add-bouncing-ball-tick-demo --strict --no-interactive
```

结果：通过，输出 `Change 'add-bouncing-ball-tick-demo' is valid`。

## 暂未完成验证

### Unity TestFramework
计划命令：
```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f2c1\Editor\Unity.exe" -batchmode -projectPath "D:\Unity_Project_1\DG\Client\DG_Client" -runTests -testPlatform EditMode -testResults "D:\Unity_Project_1\DG\Client\DG_Client\TestResults_EditMode.xml" -quit
```

当前阻塞：本机已有 Unity 进程打开同一个 `Client/DG_Client` 项目，其中一个窗口是 `DG_Client - ClientWorldRunnerDemo - Windows, Mac, Linux - Unity 2022.3.62f2c1 <DX11>`。Unity 批处理不能同时打开同一个项目，所以没有生成 `TestResults_EditMode.xml`。

解除阻塞后验证标准：
- `TestResults_EditMode.xml` 存在。
- `ClientMoveNetworkRuntime_DemoSnapshotCreatesMovingEntity` 通过。
- `ClientMoveNetworkRuntime_DemoSnapshotCreatesBoundsEntity` 通过。
- `ClientMoveNetworkRuntime_DemoChangedUpdatesCoordAndDirty` 通过。
- `ClientMoveNetworkRuntime_DemoDuplicateTickKeepsFinalCoord` 通过。
- `ClientWorldVisuals_RendersDemoBoundsWithLineRenderer` 通过。

### 双客户端手动端到端
手动验收步骤：
1. 启动服务端。
2. 打开 Unity 场景 `Client/DG_Client/Assets/Scenes/ClientWorldRunnerDemo.unity`。
3. 运行客户端 A。
4. 启动第二个 Unity 客户端或构建包作为客户端 B。
5. 确认 A 和 B 日志都出现 `[ClientBouncingDemo] observed serverTick`。
6. 确认 A 和 B 日志都出现 `[ClientBouncingDemoSnapshot] applied`，并包含运动体和边界体。
7. 确认服务端日志持续出现 `[BouncingDemoSyncSystem] broadcast`。
8. 对比服务端、A、B 的 entity id、server tick 和坐标。
9. 确认两个客户端看到同一个边界内持续反弹的球。
10. 停止服务端，确认客户端不再产生新的权威坐标。

当前结果：未执行双客户端端到端验收。
