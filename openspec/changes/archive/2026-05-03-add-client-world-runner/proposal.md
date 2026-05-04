# Change: 新增客户端世界推进器

## Why
`map-runtime-foundation` 已经把 `World` 建成地图状态和空间索引的边界，但客户端现在还没有一个最小运行时推进器来统一推进玩法逻辑。没有这个边界时，输入、移动、dirty 刷新、渲染刷新以及后续网络消息都很容易散落在不同 `MonoBehaviour` 里直接修改 `World`。

本变更只补上验证本地逻辑闭环所需的最小客户端推进器，先确认本地逻辑可以按固定 tick 稳定推进，再考虑网络、预测、插值、战斗或地图流式加载。

`Ref/Minestom` 中的服务端 tick、Instance 分层、EntityTracker target 分区、visibility difference 和 batch 边界可以作为架构参考，但本变更只迁移这些思想，不直接移植 Java 服务端源码、多线程 dispatcher、server scheduler、connection tick 或 packet flush。

## What Changes
- 新增客户端世界推进器能力，用固定逻辑 tick 推进 `World`。
- 新增运行时上下文边界，承载 `World`、tick 时间、tick 序号和命令缓冲。
- 新增客户端系统边界，让移动系统和后续系统由推进器统一调度。
- 新增移动命令缓冲，让外部代码提交意图，而不是直接修改 `World`。
- 新增移动系统，消费移动命令并调用 `World.MoveEntity`。
- 新增 dirty flush 边界，在系统运行后观察 `World.ChangedCells` 和 `World.ChangedChunks`。
- 明确参考 `Ref/Minestom` 的固定 tick、world/instance 归属、target 索引、视野差量和批量变更边界。
- 第一阶段保持本地单机逻辑，足够用于 EditMode 验证。
- 本变更不加入网络、服务端校正、预测回滚、插值、寻路、战斗、渲染实现、多线程 dispatcher 或服务端 packet flush。

## Impact
- Affected specs:
  - `client-world-runner`
  - `map-runtime-foundation`
- Affected code after approval:
  - `Client/DG_Client/Assets/Scripts/Map/Runtime/*`
  - `Client/DG_Client/Assets/Scripts/Map/WorldBootstrap.cs`
  - `Client/DG_Client/Assets/Scripts/Editor/Tests/*`
