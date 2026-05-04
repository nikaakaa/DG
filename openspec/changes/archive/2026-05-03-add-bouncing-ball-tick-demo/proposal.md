# Change: 新增配置驱动的无限 Tick 反弹演示

## Why
当前项目已经有服务端权威移动、JoinWorld、observer 广播和 Unity 客户端应用服务端结果的基础能力，但还缺一个不依赖玩家输入、能持续证明“服务端 tick -> 世界规则 -> 脏数据 -> 网络广播 -> 多客户端显示”的最小闭环。

这个演示用于先把时间循环和同步链路跑通，再继续扩展更复杂的实体、组件、Tag 和规则系统。

## What Changes
- 新增一个服务端权威的无限 Tick demo world，服务端运行期间持续推进固定 tick。
- 新增 demo 配置，由配置声明两个 entity archetype、初始组件和 tag，运行时按配置实例化 entity。
- 第一版配置生成两个 entity：一个带位置和速度的运动体，一个带边界范围的边界体；entity 本身不拥有业务名字。
- 新增或复用最小协议，让客户端 Join/Observe demo 后接收配置实例化 entity 的权威状态。
- 服务端通过 System 执行反弹规则，通过 Dirty/Sync 边界广播结果，Fantasy Handler 只保留网络入口职责。
- Unity 客户端新增最小演示场景，两个客户端连接同一服务端时显示同一颗持续反弹的球和同一个边界实体。
- 增加 Unity TestFramework 测试和手动双客户端端到端验收路径。

## Impact
- Affected specs: `bouncing-ball-tick-demo`, `authoritative-move-runner`, `client-world-runner`, `multiplayer-entity-management`, `map-runtime-foundation`
- Affected code:
  - `Tools/NetworkProtocol/Outer/OuterMessage.proto`
  - `Tools/ProtocolExportTool`
  - `Server/Entity/Generate/NetworkProtocol`
  - `Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol`
  - `Server/Hotfix/AuthoritativeMove`
  - `Server/Hotfix/Gate/Handler`
  - `Server/Tests`
  - `Client/DG_Client/Assets/Scripts/Samples`
  - `Client/DG_Client/Assets/Tests`
