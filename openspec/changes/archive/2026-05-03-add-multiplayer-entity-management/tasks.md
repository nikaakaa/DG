## 1. 协议与生成链路
- [x] 1.1 读取 `Tools/NetworkProtocol/Outer/OuterMessage.proto`，确认现有移动和 observer 协议字段。
- [x] 1.2 新增 `C2G_JoinWorldRequest // IRequest,G2C_JoinWorldResponse`。
- [x] 1.3 `G2C_JoinWorldResponse` 包含 `Success`、`EntityId`、`CurrentX`、`CurrentY`、`Reason`。
- [x] 1.4 决定是否新增 `G2C_PlayerSpawnNotify // IMessage` 或复用 `G2C_EntityMovedNotify` 做 snapshot。
- [x] 1.5 第一版未新增 spawn notify，已复用 `G2C_EntityMovedNotify` 做 Join snapshot。
- [x] 1.6 运行 `dotnet Fantasy.ProtocolExportTool.dll export --silent`。
- [x] 1.7 检查服务端生成目录包含 JoinWorld request/response 和 opcode。
- [x] 1.8 检查客户端生成目录包含 JoinWorld helper 和 opcode。
- [x] 1.9 确认没有手改任何生成的 `.cs` 文件。

## 2. 服务端多人管理器
- [x] 2.1 新增服务端最小多人管理器类型，放在服务端可构建程序集内。
- [x] 2.2 管理器维护 session 到 player entity id 的绑定。
- [x] 2.3 管理器维护 player entity id 到 session 的反向绑定。
- [x] 2.4 管理器提供分配新 player entity id 的入口。
- [x] 2.5 管理器提供查询 session 已绑定 entity 的入口。
- [x] 2.6 管理器提供清理不可用 session 的入口。
- [x] 2.7 管理器提供枚举在线 player entity snapshot 的入口。
- [x] 2.8 管理器加入世界时把 player 注册到 `AuthoritativeMoveWorld`。
- [x] 2.9 管理器为连续三个 session 分配三个不同 entity id。
- [x] 2.10 管理器不直接执行移动规则。

## 3. 服务端 JoinWorld Handler
- [x] 3.1 新增 `C2G_JoinWorldRequestHandler`。
- [x] 3.2 Handler 继承对应 `MessageRPC<TRequest,TResponse>`。
- [x] 3.3 Handler 调用多人管理器为当前 session 分配或读取 player entity。
- [x] 3.4 Handler 返回当前 session 的 player entity id 和初始坐标。
- [x] 3.5 Join 成功后把 session 加入 observer 集合。
- [x] 3.6 Join 成功后向当前 session 发送已有在线玩家 snapshot。
- [x] 3.7 Join 成功后向其他 observer 广播新玩家出现或 snapshot。
- [x] 3.8 Join 失败时返回 `Success=false` 和 `Reason`。
- [x] 3.9 Handler 日志包含 session join、entity id、初始坐标、在线玩家数量。

## 4. 移动权限与多人规则
- [x] 4.1 在 `C2G_MoveRequestHandler` 中读取当前 session 绑定的 player entity。
- [x] 4.2 未 Join 的 session 发送移动请求时返回失败。
- [x] 4.3 request.EntityId 与绑定 entity 不一致时返回失败。
- [x] 4.4 合法请求仍调用 `AuthoritativeMoveWorld.Move(...)`。
- [x] 4.5 合法移动仍先回复 `G2C_MoveResponse`，再广播 notify。
- [x] 4.6 越权移动不广播 `G2C_EntityMovedNotify`。
- [x] 4.7 为多人移动补玩家占位冲突规则，避免两个 player entity 进入同一格。
- [x] 4.8 玩家占位冲突失败时返回明确业务错误码和 reason。
- [x] 4.9 日志区分未 Join、越权、未知 entity、阻挡格、占位冲突和成功移动。

## 5. 服务端验证
- [x] 5.1 扩展 `AuthoritativeMoveVerification` 或新增等价验证入口。
- [x] 5.2 验证三个模拟 session 分配到三个不同 entity id。
- [x] 5.3 验证重复 Join 同一 session 不重复分配 entity。
- [x] 5.4 验证 session A 不能移动 session B 的 entity。
- [x] 5.5 验证未 Join session 不能移动任何 entity。
- [x] 5.6 验证三名玩家初始坐标互不重叠。
- [x] 5.7 验证玩家占位格拒绝移动。
- [x] 5.8 验证断开或不可用 session 不会被广播枚举使用。
- [x] 5.9 运行 `dotnet build Server/Server.sln -v minimal`。
- [x] 5.10 运行服务端验证命令并记录期望输出。

## 6. Unity 客户端 Join 主路径
- [x] 6.1 新增或调整客户端 JoinWorld 调用入口。
- [x] 6.2 `ClientWorldDemo` 初始化时等待 session 可用后先 JoinWorld。
- [x] 6.3 Join 成功后用响应 `EntityId` 创建或绑定本地 player entity。
- [x] 6.4 移动输入只提交本地绑定 player entity。
- [x] 6.5 默认 Inspector `localEntityId` 不再决定多人主路径身份。
- [x] 6.6 保留 `--entityId` 仅作为调试覆盖时要有清晰日志。
- [x] 6.7 Join 失败时不允许进入移动提交。
- [x] 6.8 客户端日志包含 Join 成功 entity id 和初始坐标。

## 7. Unity 客户端远端玩家表现
- [x] 7.1 收到其他玩家 snapshot 或 notify 时可创建远端 player entity。
- [x] 7.2 本地 player 和远端 player 在 world 中使用不同 entity id。
- [x] 7.3 `ClientWorldVisuals` 能显示多个 player entity。
- [x] 7.4 远端 player 更新走服务端最终坐标，不执行本地移动规则。
- [x] 7.5 重复 snapshot 或重复 notify 保持幂等。
- [x] 7.6 缺失 runner 或 world 时输出失败日志，不创建第二个 world。

## 8. Unity TestFramework
- [x] 8.1 增加 EditMode 测试：Join 响应能创建本地 player entity。
- [x] 8.2 增加 EditMode 测试：两个不同 entity id 可同时存在于客户端 world。
- [x] 8.3 增加 EditMode 测试：远端 notify 能创建或更新远端 player entity。
- [x] 8.4 增加 EditMode 测试：重复 notify 保持最终坐标稳定。
- [x] 8.5 增加 EditMode 测试：移动提交使用本地绑定 entity id。
- [x] 8.6 运行 Unity EditMode 测试并记录通过数量。

## 9. 构建验证
- [x] 9.1 运行 `dotnet build .\Client\DG_Client\Assembly-CSharp.csproj --no-restore -v minimal`。
- [x] 9.2 运行 `dotnet build .\Client\DG_Client\Assembly-CSharp-Editor.csproj --no-restore -v minimal`。
- [x] 9.3 运行 `dotnet build Server/Server.sln -v minimal`。
- [x] 9.4 运行 `openspec validate add-multiplayer-entity-management --strict --no-interactive`。

## 10. 三客户端手动端到端验证
- [x] 10.1 启动服务端，确认 Gate KCP 20000 监听。
- [x] 10.2 启动客户端 A，确认 Join 成功并分配 entity A。
- [x] 10.3 启动客户端 B，确认 Join 成功并分配 entity B。
- [x] 10.4 启动客户端 C，确认 Join 成功并分配 entity C。
- [x] 10.5 服务端日志确认 A/B/C 是三个不同 entity。
- [x] 10.6 三个客户端画面或日志确认存在三个 player entity。
- [x] 10.7 操作 A 移动，B 和 C 能看到 A 的 entity 更新。
- [x] 10.8 操作 B 移动，A 和 C 能看到 B 的 entity 更新。
- [x] 10.9 操作 C 移动，A 和 B 能看到 C 的 entity 更新。
- [x] 10.10 尝试让 A 移动到 B 当前格，服务端拒绝且不广播错误坐标。
- [x] 10.11 关闭 B 后继续操作 A，服务端不会向 B 的 disposed session 发送广播。
- [x] 10.12 把结果回填到 `add-authoritative-move-broadcast` 第 9 节或新归档说明中。

## 验证记录
- [x] 协议导出：`dotnet Fantasy.ProtocolExportTool.dll export --silent` 已成功生成 JoinWorld 协议、opcode 和客户端 helper。
- [x] 服务端构建：`dotnet build Server/Server.sln -v minimal` 通过，2 个既有 nullable 警告，0 错误。
- [x] 服务端规则验证：`dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj` 输出 `Authoritative move verification passed.`。
- [x] 客户端运行时代码构建：`dotnet build .\Client\DG_Client\Assembly-CSharp.csproj --no-restore -v minimal` 通过，20 个既有程序集版本冲突警告，0 错误。
- [x] 客户端 EditMode 程序集构建：`dotnet build .\Client\DG_Client\Assembly-CSharp-Editor.csproj --no-restore -v minimal` 通过，21 个既有程序集版本冲突警告，0 错误。
- [x] OpenSpec 校验：`openspec validate add-multiplayer-entity-management --strict --no-interactive` 通过。
- [x] Unity TestFramework 执行：用户已确认测试完毕，可进入归档。
- [x] 三客户端端到端：用户已确认测试完毕，可进入归档。
