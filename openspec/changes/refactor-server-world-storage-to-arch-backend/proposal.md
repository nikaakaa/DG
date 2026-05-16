# Change: 服务端权威 GameWorld 接入 Arch ECS Backend

## Why

DG 已经把权威状态收口到 `GameWorld` 和 `IWorldDataStorage` 边界，但当前默认实现仍是自建 indexed storage。下一步需要把服务端权威链路直接接入 Arch ECS backend，让服务端世界实体、组件和查询进入真正 archetype/chunk ECS 存储，同时保持 Fantasy、规则层、协议层和客户端镜像的统一权威边界不变。

本变更不是重新讨论是否接 Arch，而是把接入方式定成最终架构：Fantasy 负责服务端外壳，Arch 负责服务端权威 `GameWorld` 内部 ECS 存储，Shared GameCore 继续提供统一规则语义，Unity 客户端只消费服务端 snapshot/delta 和表现 metadata。

## What Changes

- 在服务端权威 `GameWorld` 下新增 Arch ECS storage backend，并让服务端默认使用该 backend 创建权威世界。
- 保留 `IWorldDataStorage` 作为唯一存储边界，规则层、协议层、Fantasy Handler、Unity 客户端不得直接引用 `Arch.World`、`Arch.Entity` 或 Arch query 类型。
- 建立稳定的 `EntityId <-> Arch.Entity` 映射，协议、日志、WorldDelta、snapshot、session ownership 继续只使用 DG `EntityId`。
- 将 `AddEntity`、`RemoveEntity`、`SetComponent`、`RemoveComponent`、`HasComponent`、`TryGetComponent`、`QueryEntities` 接到 Arch backend。
- 将自动 push source、`QueryPushOnEnter` 和真实规则热路径组件组合查询下沉到 Arch backend 能力，而不是回退为全实体扫描。
- 将大规模只读扫描和候选计算从串行提交路径中抽出，形成 Arch query / parallel candidate collection / deterministic commit 三段式权威 tick。
- 首批并行候选计算覆盖 auto push source readiness、push-on-enter source scan、runtime effect expiry candidate scan、observer-independent changed entity snapshot materialization。
- 保持 spatial index、dirty journal、runtime effect final component、WorldSnapshot、WorldDelta 和 animation metadata 由 DG GameCore 控制。
- 将服务端权威 tick 的节拍、移动速度和同步速度统一写入模型：服务端 tick 是唯一结算节拍，格子移动以 action cost / ready tick 结算，客户端表现速度只能跟随服务端 delta metadata。
- 维持 Unity 客户端镜像边界：客户端不运行 Arch 权威裁决，不复制服务端规则，只应用服务端 snapshot/delta。
- 补齐 Unity TestFramework EditMode、Shared/Server verification 和用户手动 Play Mode + 双客户端端到端验证口径。

## Non-Goals

- 不让规则层直接写 Arch API。
- 不让 Fantasy Entity 成为 DG 玩法实体。
- 不把服务端改成 Unity Headless。
- 不把 Unity DOTS 引入 Shared GameCore。
- 不在客户端建立第二套权威 Arch World。
- 不改变 Outer 协议的权威语义。
- 不重写 ActionSpec、blocked/push/connected body/runtime effect 的规则含义。
- 不把空间 chunk 和 ECS archetype chunk 合并成一个概念。
- 不让并行 worker 直接提交坐标、dirty、WorldDelta、session broadcast 或 runtime effect final component。

## Impact

- Affected specs:
  - `shared-gamecore-entity-rules`
  - `authoritative-move-runner`
  - `client-world-runner`
- Affected code:
  - `Shared/DG.GameCore/DG.GameCore.csproj`
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/World/WorldDataStorage.cs`
  - `Shared/DG.GameCore/World/WorldStorageQuery.cs`
  - `Shared/DG.GameCore/Spatial/SpatialEntityIndex.cs`
  - `Shared/DG.GameCore/Rules/*`
  - `Server/Hotfix/AuthoritativeMove/World/AuthoritativeMoveWorldProvider.cs`
  - `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
  - `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`
  - Unity EditMode tests under `Client/DG_Client/Assets/Tests/Editor`

## Verification

- `openspec validate refactor-server-world-storage-to-arch-backend --strict --no-interactive`
- `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- `dotnet build Server/Server.sln -v minimal`
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode targeted tests for GameWorld storage equivalence, server-authoritative world runner, client mirror delta application, and animation metadata consumption.
- 用户手动验证：启动 Fantasy 服务端，Unity Play Mode 启动两个客户端，覆盖 JoinWorld、玩家移动、自动源 push 输出、机关推动、connected body、debug spawn/move/remove、runtime effect，并确认两个客户端只跟随同一服务端 `WorldDelta` 序列。
