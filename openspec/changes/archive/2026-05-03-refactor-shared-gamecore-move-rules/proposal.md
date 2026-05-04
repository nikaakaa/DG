# Change: 重构共享 GameCore 与统一移动规则

## Why
当前玩家 demo 和反弹球 demo 已经证明了服务端权威移动、固定 tick、observer 广播和 Unity 客户端应用服务端状态，但实现上分裂成 `AuthoritativeMoveWorld` 与 `BouncingDemoWorld` 两套世界模型。玩家、球、边界不在同一个服务端空间内，导致“球碰到玩家反弹”这类物体间规则不能自下而上产生，只能跨 demo 特判。

本变更把底层规则收敛为共享 GameCore：实体由组件/tag/配置组成，移动来源统一产生 MoveCommand，服务端通过一个权威 GameWorld 裁决移动、碰撞、反弹和 dirty 同步，客户端只镜像服务端结果。

## What Changes
- 新增 `Shared/DG.GameCore` 纯 C# 规则内核，定义统一实体、坐标、组件、tag、配置、空间占用、MoveCommand、MoveResult、DirtyChange 和 Snapshot/Delta 数据模型。
- 将玩家 demo 与反弹球 demo 替换为同一个服务端权威 GameWorld，不再让玩家和球分别存在于不同 world。
- 用 `DirectionComponent`、`AutoMoveComponent`、`BouncableComponent`、`ColliderComponent`、`BlockingComponent` 等组件表达反弹球和玩家能力，不引入 Velocity 作为第一版底层概念。
- 将玩家输入、自动 tick、后续传送带/推进器等移动来源统一为 MoveCommand，由 MovementResolveSystem 统一裁决。
- 服务端 Fantasy Handler 继续只负责网络入口、session/entity 绑定和命令提交，不直接执行碰撞或反弹规则。
- 移除 demo 专用同步路径，用统一 WorldSnapshot/WorldDelta 表达玩家、球、阻挡体的服务端权威状态。
- Unity 客户端保留当前可用场景入口，但网络状态应用改为统一 snapshot/delta，并按组件/tag 显示玩家、球和阻挡体。
- 增加服务端规则验证、Unity TestFramework 验证和手动端到端验收，证明球碰玩家反弹、玩家移动占位、统一同步和双客户端显示。

## Impact
- Affected specs:
  - `shared-gamecore-entity-rules`
  - `authoritative-move-runner`
  - `multiplayer-entity-management`
  - `client-world-runner`
  - 本变更替代 `add-bouncing-ball-tick-demo` 中 demo 专用 world 和 demo 专用同步方向
- Affected code:
  - `Shared/DG.GameCore`
  - `Tools/NetworkProtocol/Outer/OuterMessage.proto`
  - `Tools/ProtocolExportTool`
  - `Server/Entity/Generate/NetworkProtocol`
  - `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol`
  - `Server/Hotfix/AuthoritativeMove`
  - `Server/Hotfix/BouncingDemo`
  - `Server/Hotfix/Gate/Handler`
  - `Server/Tests/AuthoritativeMoveVerification`
  - `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo`
  - `Client/DG_Client/Assets/Scripts/Map`
  - `Client/DG_Client/Assets/Tests`
