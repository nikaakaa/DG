# Change: 增加多人玩家实体管理系统

## Why
当前移动广播链路已经能把服务端裁决结果推给在线 observer，但客户端默认都使用 `localEntityId = 1`，服务端也没有“连接进入后分配玩家 entity”的统一边界。结果是多个客户端默认操作同一个角色，三客户端以上时只是更多 session 争用同一个 entity，而不是真正的多玩家。

需要先补一个最小多人管理系统，把 Fantasy `Session`、玩家身份、服务端 entity id、客户端本地 player entity、observer 注册和移动权限绑定起来，再继续验证双客户端或多客户端同步。

## What Changes
- 新增服务端多人玩家管理器，负责分配/绑定 player entity，并维护 session 到 entity 的归属关系。
- 新增或调整进入世界协议，让客户端连接后向服务端请求自己的 player entity，而不是依赖命令行或 Inspector 硬编码 id。
- 移动请求必须使用当前 session 已绑定的 player entity；未绑定、未知 entity、越权操作其他玩家 entity 都要被拒绝。
- observer 广播从“客户端声明观察某个 entity”收敛为“加入世界后默认观察当前共享世界”，仍保留后续 AOI 扩展空间。
- Unity 客户端初始化时根据服务端分配结果创建本地玩家，并能在收到其他玩家 snapshot/notify 时创建或更新远端玩家表现。
- 增加 Unity TestFramework 覆盖客户端本地世界中的多玩家实体创建/更新，增加服务端验证程序或测试覆盖分配、绑定、越权拒绝和三客户端管理。
- 明确端到端手动验证：启动服务端和至少三个客户端，每个客户端获得不同 entity，移动任意一个客户端时其他客户端可看到对应 entity 更新。

## Impact
- Affected specs: `multiplayer-entity-management`
- Related active changes: `add-authoritative-move-broadcast`
- Affected code: `Tools/NetworkProtocol/Outer/OuterMessage.proto`, `Tools/ProtocolExportTool`, `Server/Entity/AuthoritativeMove`, `Server/Hotfix/AuthoritativeMove`, `Server/Hotfix/Gate/Handler`, `Server/Tests/AuthoritativeMoveVerification`, `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo`, `Client/DG_Client/Assets/Tests`
- Verification: `openspec validate add-multiplayer-entity-management --strict --no-interactive`, protocol export, server build, client build, Unity EditMode tests, server verification program, three-client manual run
