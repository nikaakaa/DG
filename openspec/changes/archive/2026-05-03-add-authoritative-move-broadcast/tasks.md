## 1. 协议与生成链路
- [x] 1.1 读取 `Tools/NetworkProtocol/Outer/OuterMessage.proto`，确认当前移动 RPC 字段不需要修改。
- [x] 1.2 在协议源中新增最小 observer 注册请求与响应。
- [x] 1.3 observer 注册请求字段包含 `EntityId`。
- [x] 1.4 observer 注册响应能表达注册成功和当前 entity 初始坐标。
- [x] 1.5 在协议源中新增 `G2C_EntityMovedNotify // IMessage`。
- [x] 1.6 moved notify 字段包含 `EntityId`、`FinalX`、`FinalY`、`ServerTick`、`ClientTick`。
- [x] 1.7 运行 `dotnet Fantasy.ProtocolExportTool.dll export --silent`。
- [x] 1.8 检查服务端生成目录出现 observer 注册协议、`G2C_EntityMovedNotify` 和 opcode。
- [x] 1.9 检查客户端生成目录出现 observer 注册 helper、`G2C_EntityMovedNotify` 和 opcode。
- [x] 1.10 确认没有手改任何生成的 `.cs` 文件。

## 2. 服务端在线连接集合
- [x] 2.1 确定最小集合类型放置位置，保持在服务端可构建程序集内。
- [x] 2.2 将 observer/session 集合与 entity owner 归属记录拆成两个概念。
- [x] 2.3 增加 session 进入 observer 集合的注册入口。
- [x] 2.4 增加 entity id 到 owner session 的刷新入口。
- [x] 2.5 增加 session 或 entity owner 的移除入口。
- [x] 2.6 增加枚举当前 observer 的纯数据入口，便于测试。
- [x] 2.7 枚举时跳过或清理已不可用的 observer。
- [x] 2.8 增加最小纯规则验证，证明注册两个 observer 后可枚举两个目标。
- [x] 2.9 增加最小纯规则验证，证明 entity owner 刷新不会覆盖 observer 集合。
- [x] 2.10 增加最小纯规则验证，证明不可用 observer 不会被广播使用。

## 3. 服务端 observer 注册 Handler
- [x] 3.1 在 `Server/Hotfix` 下按项目约定创建 observer 注册 RPC Handler。
- [x] 3.2 Handler 继承对应的 `MessageRPC<TRequest, TResponse>`。
- [x] 3.3 Handler 从请求读取 `EntityId`。
- [x] 3.4 Handler 将当前 Fantasy `Session` 加入 observer 集合。
- [x] 3.5 Handler 刷新 entity owner 归属记录。
- [x] 3.6 Handler 返回注册成功和当前服务端坐标。
- [x] 3.7 Handler 记录注册日志，包含 entity id 和 observer 数量。

## 4. 服务端移动广播
- [x] 4.1 在 `C2G_MoveRequestHandler` 收到请求时刷新 entity owner，但不依赖移动请求作为唯一 observer 注册来源。
- [x] 4.2 保持 Handler 调用 `AuthoritativeMoveWorld.Move(...)` 作为唯一裁决入口。
- [x] 4.3 合法移动时填充 `G2C_MoveResponse`。
- [x] 4.4 合法移动时先调用 `reply()` 回复发起者。
- [x] 4.5 合法移动时构造 `G2C_EntityMovedNotify`，坐标来自 `MoveResult.FinalCoord`。
- [x] 4.6 合法移动时向在线 observer/session 集合广播 notify，包含发起者。
- [x] 4.7 非法移动时只回复 `G2C_MoveResponse Success=false`。
- [x] 4.8 非法移动时不发送 `G2C_EntityMovedNotify`。
- [x] 4.9 增加日志，能区分 response、broadcast、skip broadcast 三类结果。
- [x] 4.10 运行 `dotnet build Server/Server.sln -v minimal`。

## 5. 服务端验证程序或测试
- [x] 5.1 扩展现有 `AuthoritativeMoveVerification` 或新增最小验证入口。
- [x] 5.2 验证 observer 注册后才会被枚举为广播目标。
- [x] 5.3 验证 B 只注册不移动也会被选为 observer。
- [x] 5.4 验证合法移动生成一条 moved notify 数据。
- [x] 5.5 验证两个在线 observer 都会被选为广播目标。
- [x] 5.6 验证阻挡格失败不会生成 moved notify。
- [x] 5.7 验证 unknown player 或不可用 observer 不会导致验证进程异常。
- [x] 5.8 运行服务端验证命令并记录期望输出。

## 6. Unity 客户端 observer 注册
- [x] 6.1 确认客户端生成协议中存在 observer 注册 helper。
- [x] 6.2 在 demo 初始化或连接可用后发送 observer 注册请求。
- [x] 6.3 注册请求使用当前 demo entity id。
- [x] 6.4 注册成功后按响应坐标校准本地 world。
- [x] 6.5 注册失败时输出可验证日志，不直接进入双客户端验证。
- [x] 6.6 确认 B 不需要先移动也能完成 observer 注册。

## 7. Unity 客户端 push handler
- [x] 7.1 确认客户端生成协议中存在 `G2C_EntityMovedNotify`。
- [x] 7.2 按 Fantasy Unity 约定新增 `Message<Session, G2C_EntityMovedNotify>` handler。
- [x] 7.3 handler 通过现有 demo/runner 接入点找到当前 `ClientWorldRunner`。
- [x] 7.4 handler 收到 notify 后按 `EntityId`、`FinalX`、`FinalY` 应用本地 world。
- [x] 7.5 handler 不直接执行本地移动规则。
- [x] 7.6 handler 不创建第二个 `World`。
- [x] 7.7 重复收到同一最终坐标时保持幂等，不把客户端坐标推到错误位置。
- [x] 7.8 增加客户端日志 `[ClientMoveNotify] applied` 和失败日志，便于双客户端验证。

## 8. Unity 客户端测试
- [x] 8.1 增加 EditMode 测试，证明 notify 能更新已存在 entity。
- [x] 8.2 增加 EditMode 测试，证明 notify 指向缺失 entity 时不抛异常。
- [x] 8.3 增加 EditMode 测试，证明重复 notify 保持最终坐标稳定。
- [x] 8.4 增加 EditMode 测试，证明 notify 应用仍进入 dirty flush 链路。
- [x] 8.5 增加 EditMode 测试或结构验证，证明 observer 注册不创建第二个 world。
- [x] 8.6 运行 Unity EditMode 测试或等价项目测试命令。

## 9. 双客户端运行验证
- [x] 9.1 启动服务端，确认 Gate KCP 20000 监听。
- [x] 9.2 启动客户端 A，确认 Fantasy session 可用。
- [x] 9.3 启动客户端 B，确认 Fantasy session 可用。
- [x] 9.4 在服务端日志确认 A 完成 observer 注册。
- [x] 9.5 在服务端日志确认 B 完成 observer 注册。
- [x] 9.6 在 B 未发送移动请求的前提下，让 A 发送合法移动到可进入坐标。
- [x] 9.7 在服务端日志确认 `C2G_MoveRequestHandler` 命中并记录 observer 数量为 2。
- [x] 9.8 在 A 客户端确认收到成功 `G2C_MoveResponse`。
- [x] 9.9 在 A 客户端确认收到或应用 `G2C_EntityMovedNotify`。
- [x] 9.10 在 B 客户端确认收到 `G2C_EntityMovedNotify`。
- [x] 9.11 在 B 客户端确认同一 entity 坐标更新为服务端最终坐标。
- [x] 9.12 让 A 移动到阻挡格 `(2,0)`。
- [x] 9.13 在 A 客户端确认 `Success=false`、`MoveErrorCode=2`、`Reason=blocked cell`。
- [x] 9.14 在服务端日志确认非法移动 skip broadcast。
- [x] 9.15 在 B 客户端确认没有应用新的 moved notify，坐标保持不变。

## 10. OpenSpec 与收尾验证
- [x] 10.1 运行 `openspec validate add-authoritative-move-broadcast --strict --no-interactive`。
- [x] 10.2 记录所有验证命令和预期通过标准。
- [x] 10.3 按验证事实逐项勾选任务，不把构建通过等同于双客户端同步完成。

## 自动验证记录
- `dotnet Fantasy.ProtocolExportTool.dll export --silent`，工作目录 `Tools/ProtocolExportTool`，预期输出包含 `成功: 已从 ExporterSettings.json 加载配置。`
- `dotnet build Server/Server.sln -v minimal`，预期 `0 个错误`，本次结果 `0 个警告，0 个错误`。
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj`，预期输出 `Authoritative move verification passed.`。
- `dotnet build .\Client\DG_Client\Assembly-CSharp.csproj --no-restore -v minimal`，预期 `0 个错误`，本次结果 `20 个警告，0 个错误`。
- `dotnet build .\Client\DG_Client\Assembly-CSharp-Editor.csproj --no-restore -v minimal`，预期 `0 个错误`，本次结果 `21 个警告，0 个错误`。
- Unity MCP EditMode `DG.EditorTests.ClientWorldRunnerTests`，预期全部通过，本次结果 `21/21 passed`。
- `openspec validate add-authoritative-move-broadcast --strict --no-interactive`，预期输出 `Change 'add-authoritative-move-broadcast' is valid`。
- 第 9 章为手动双客户端运行验证，当前未用自动验证替代。
