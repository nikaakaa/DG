# Change: 收口 GameWorld 存储适配器边界

## Why

`refactor-authoritative-world-data-oriented-storage` 已经把 `GameWorld` 的实体、组件、查询和 dirty 入口收向内部 `WorldDataStorage`，但当前 `GameWorld` 仍直接持有具体 storage 实现。继续重构底层 ECS 前，需要先把存储实现收成可替换 adapter 边界，避免后续评估 Arch 或其他存储时污染规则层、Fantasy Hotfix 层和协议同步层。

本变更只定义并实施 `GameWorld -> IWorldDataStorage -> IndexedWorldDataStorage` 的内部边界。Arch 只能作为后续独立提案中的候选实现，不在本变更引入。

## What Changes

- 在 Shared GameCore 内部建立 `IWorldDataStorage` 或等价 storage adapter 合同。
- 将当前 `WorldDataStorage` 明确为 indexed/dense-pool 实现，并从 `GameWorld` 的具体依赖中抽出。
- 保持 `GameWorld` public API、规则语义、snapshot/delta 语义和 Fantasy 服务端调用方式不变。
- 明确规则层、runner、planner、commit、runtime effect、sync system 不得直接引用具体 storage、pool、row、mask、query cache 或第三方 ECS 类型。
- 为后续 Arch 小切片保留替换口，但不引入 Arch 包、不实现 `ArchWorldDataStorage`。
- 增加自动化测试，证明当前 indexed adapter 与现有语义一致，并证明外部调用方只依赖 `GameWorld` public API。

## Non-Goals

- 不引入 Arch、Unity DOTS、DefaultEcs、Flecs 或其他 ECS 包。
- 不实现 archetype/chunk ECS。
- 不新增 system scheduler。
- 不改变 `ActionSpec`、Action/Effect、push、connected body、runtime effect 或 AutoMove self push 语义。
- 不改变 Outer 协议、`WorldDelta` 字段或客户端 mirror 应用逻辑。
- 不把 Fantasy `Entity` / `Scene` 作为 GameCore 规则实体。
- 不重写临时裁决集合、observer/session ownership、配置查找或空间索引。

## Impact

- Affected specs:
  - `shared-gamecore-entity-rules`
- Affected code:
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/World/WorldDataStorage.cs`
  - `Shared/DG.GameCore/World/WorldStorageQuery.cs`
  - `Shared/DG.GameCore/Rules/*`
  - `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
  - `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`
  - Unity EditMode tests under `Client/DG_Client/Assets/Tests/Editor`

## Verification

- `openspec validate refactor-world-storage-adapter-boundary --strict --no-interactive`
- `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode targeted tests for GameWorld storage behavior and client mirror snapshot/delta apply.
- 用户手动验证：服务端 + Unity Play Mode + 双客户端，确认 join、移动、自动移动、推动、debug edit 和 runtime effect 仍只通过服务端 `WorldDelta` 收敛。
