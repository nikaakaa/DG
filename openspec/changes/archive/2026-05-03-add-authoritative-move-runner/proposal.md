# Change: 增加服务端权威移动推进器

## Why
当前客户端已经有本地 `ClientWorldRunner`、`MovementSystem` 和 `SubmitMovement` 链路，可以在 Unity 内按固定 tick 推进本地 `World`。但移动仍然由客户端本地规则直接落地，服务端没有对应的权威推进器、移动 Handler 和最小世界状态，无法验证“客户端发移动意图、服务端裁决、客户端按响应更新”的闭环。

本变更把移动链路推进到服务端权威模式的最小可运行切片：客户端继续保留现有 runner 的输入和表现边界，服务端新增自己的推进器和世界状态，移动必须经过协议请求与响应确认后再更新客户端世界。

## What Changes
- 增加服务端权威移动推进器能力，服务端维护玩家坐标、阻挡格和移动规则。
- 增加移动协议生成链路要求，移动消息必须从 `.proto` 导出到服务端和客户端，不能手写生成文件。
- 增加 `C2G_MoveRequest` 的服务端 RPC Handler 要求，Handler 只负责接请求、调用推进器、填响应和记录日志。
- 增加客户端网络移动接入要求，客户端按键后发送移动请求，收到成功响应后才更新本地 `World`。
- 明确第一阶段不做预测、插值、回滚、多玩家广播、复杂地图加载、跨服路由和 Unity 客户端 `World` 复用到服务端。

## Impact
- Affected specs:
  - `authoritative-move-runner`
  - `client-world-runner`
- Affected code after approval:
  - `Tools/NetworkProtocol/Outer/OuterMessage.proto`
  - `Tools/ProtocolExportTool/ExporterSettings.json`
  - `Server/Entity/Generate/NetworkProtocol/*`
  - `Server/Entity/**`
  - `Server/Hotfix/**`
  - `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol/*`
  - `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/**`
  - `Client/DG_Client/Assets/Scripts/Map/Runtime/**`

