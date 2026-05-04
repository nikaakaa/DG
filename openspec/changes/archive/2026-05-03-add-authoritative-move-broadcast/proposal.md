# Change: 增加服务端权威移动广播

## Why
当前单客户端权威移动闭环已经打通，但服务端只回复发起者，其他在线客户端不会收到同一 entity 的最终坐标。下一步需要一个最小双客户端同步切片：A 移动由服务端裁决后，A 和 B 都能通过同一份服务端结果更新本地 world。

## What Changes
- 在 Outer 协议中新增 `G2C_EntityMovedNotify // IMessage`，用于服务端主动推送已裁决成功的移动结果。
- 服务端维护最小在线 observer/session 集合，并保留 entity id 与拥有者 session 的最小归属记录。
- 客户端通过最小上线/观察者注册请求进入 observer 集合，避免只连接但未移动的 B 收不到 A 的广播。
- `C2G_MoveRequestHandler` 在成功移动后先回复发起者，再向在线集合广播移动通知。
- 非法移动只通过 `G2C_MoveResponse` 回复发起者失败，不广播 `G2C_EntityMovedNotify`。
- Unity 客户端增加服务端 push handler，收到 `G2C_EntityMovedNotify` 后把最终坐标应用到本地 world。
- 增加双客户端验证路径，覆盖 A 合法移动后 B 更新，以及 A 非法移动后 B 不更新。

## Impact
- Affected specs: `authoritative-move-runner`, `client-world-runner`
- Affected code: `Tools/NetworkProtocol/Outer/OuterMessage.proto`, `Tools/ProtocolExportTool`, `Server/Entity/Generate/NetworkProtocol`, `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol`, `Server/Hotfix/Gate/Handler/C2G_MoveRequestHandler.cs`, `Server/Hotfix/Gate/Handler`, `Server/Entity/AuthoritativeMove`, `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo`
- Reference boundary: 只借鉴 Minestom 的 server process 阶段顺序、Instance facade、EntityTracker observer/update 差量和 Batch 统一 flush 思想；不移植 Minecraft 协议、AOI view distance、线程调度、packet 系统、完整事件总线、预测、回滚或插值。
