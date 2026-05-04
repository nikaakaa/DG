## 1. 协议与生成链路
- [x] 1.1 检查 `Tools/ProtocolExportTool/ExporterSettings.json` 指向当前项目真实路径。
- [x] 1.2 检查 `Tools/NetworkProtocol/Outer/OuterMessage.proto` 是否定义 `C2G_MoveRequest` 和 `G2C_MoveResponse`。
- [x] 1.3 确认响应字段不与 `IResponse.ErrorCode` 冲突，业务错误使用 `MoveErrorCode`。
- [x] 1.4 在 `Tools/ProtocolExportTool` 运行 `dotnet Fantasy.ProtocolExportTool.dll export --silent`。
- [x] 1.5 检查服务端生成目录出现 `C2G_MoveRequest`、`G2C_MoveResponse` 和 opcode。
- [x] 1.6 检查客户端生成目录出现 `NetworkProtocolHelper.C2G_MoveRequest(...)`。

## 2. 服务端构建闸门
- [x] 2.1 运行 `dotnet build Server/Server.sln -v minimal` 记录当前构建状态。
- [x] 2.2 如果构建失败，优先修复阻塞完整服务端构建的基础问题。
- [x] 2.3 再次运行 `dotnet build Server/Server.sln -v minimal`，确认添加业务前服务端可构建。

## 3. 服务端最小世界状态
- [x] 3.1 确定服务端最小世界类型放置位置，不引用 Unity 客户端程序集。
- [x] 3.2 增加玩家坐标状态，支持按 `playerId` 查询当前坐标。
- [x] 3.3 增加阻挡格状态，支持设置和查询不可进入坐标。
- [x] 3.4 增加 `CanEnter` 规则，拒绝阻挡格和非法目标。
- [x] 3.5 增加 `Move` 入口，成功时更新坐标，失败时返回原坐标和错误原因。
- [x] 3.6 添加最小测试或可执行验证，覆盖合法移动成功。
- [x] 3.7 添加最小测试或可执行验证，覆盖阻挡格移动失败。

## 4. 服务端移动 Handler
- [x] 4.1 在 `Server/Hotfix` 下按项目约定创建 `C2G_MoveRequestHandler`。
- [x] 4.2 Handler 继承 `MessageRPC<C2G_MoveRequest, G2C_MoveResponse>`。
- [x] 4.3 Handler 从请求读取 `EntityId`、目标坐标和 `ClientTick`。
- [x] 4.4 Handler 调用服务端推进器 `Move`，不直接写世界状态细节。
- [x] 4.5 成功时填充 `Success`、`EntityId`、`FinalX`、`FinalY`、`ClientTick`。
- [x] 4.6 失败时填充 `Success=false`、最终坐标、`MoveErrorCode` 和 `Reason`。
- [x] 4.7 Handler 记录请求和结果日志，便于 Unity 运行时验证。
- [x] 4.8 运行 `dotnet build Server/Server.sln -v minimal`。

## 5. 客户端网络接入
- [x] 5.1 检查 `Client/DG_Client/Packages/manifest.json` 是否具备 Fantasy.Unity 依赖。
- [x] 5.2 检查 Unity 编译符号是否具备 `FANTASY_UNITY`。
- [x] 5.3 确定客户端使用 `FantasyRuntime`、`Runtime.Connect` 或 `scene.Connect` 的一种连接方式。
- [x] 5.4 增加客户端 Session 获取边界，移动提交前必须能判断 Session 是否可用。
- [x] 5.5 将 demo 按键输入改为发送 `C2G_MoveRequest`，传入实体 id、目标坐标和当前 tick。
- [x] 5.6 收到成功响应后，按响应最终坐标更新本地 world。
- [x] 5.7 收到失败响应后，保持原坐标并输出 `MoveErrorCode` 与 `Reason`。
- [x] 5.8 保留本地 runner 的固定 tick 推进和 dirty flush 形状。

## 6. 端到端验证
- [x] 6.1 运行协议导出并确认服务端和客户端生成结果一致。
- [x] 6.2 运行 `dotnet build Server/Server.sln -v minimal`。
- [x] 6.3 启动服务端并确认 Gate 监听 KCP 20000。
- [x] 6.4 在 Unity 中打开客户端 demo 场景并确认无编译错误。
- [x] 6.5 运行 demo，按键触发移动请求。
- [x] 6.6 在服务端日志确认 `C2G_MoveRequestHandler` 被命中。
- [x] 6.7 在客户端确认收到 `G2C_MoveResponse`。
- [x] 6.8 验证合法移动后客户端坐标更新为服务端最终坐标。
- [x] 6.9 验证非法移动后客户端坐标保持不变。
