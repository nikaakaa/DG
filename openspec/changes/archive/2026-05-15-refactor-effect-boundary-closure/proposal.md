# Change: 收口 Effect 应用边界

## Why
`add-effect-application-layer` 已经建立 `EffectSpec -> EffectApplication -> Commit -> RuntimeEffectStore -> ComponentStateResolver` 主链路，但仍存在可绕过 commit 的公共入口、客户端本地 effect 应用分支、服务端 debug 手写 effect adapter、以及 tag/component proposal 语义混杂的问题。

本变更把 effect 作为唯一的运行时 component/tag source 叠加层收口：所有 runtime effect 和 runtime component/tag contribution 都必须通过 commit 接受、通过 source key 保存、通过 resolver 结算，并由客户端只镜像服务端 final result。

## What Changes
- **BREAKING** 收紧 `GameWorld` runtime effect 写入口：普通运行时代码和测试不得直接通过公共 `AddRuntimeEffect` / `RemoveRuntimeEffect` 绕过 commit。
- 统一 runtime component/tag contribution 模型，让 `SetComponentResult`、`AddTag`、`RemoveTag` 不直接写 final component/tag，而是写入或移除明确 source。
- 服务端 debug runtime effect 改为引用 Luban-backed `EffectSpec` 或明确的 debug effect spec registry，不再从 `RuntimeEffectKind` 临时反推手写 `EffectSpec` 作为主路径。
- 客户端 server-authoritative 模式移除或隔离本地 runtime effect 应用/移除分支；调试按钮只提交 RPC，镜像状态只来自 server snapshot/delta。
- 增加按 entity 移除 runtime effect source 的明确能力，用于还原对象 runtime effect 叠加状态。
- 增加自动化测试和源码边界扫描，验证 effect 增删乱序、stack、tag add/remove、static source 保留、客户端不本地解析 effect。

## Impact
- Affected specs:
  - `runtime-component-results`
  - `client-world-runner`
  - `authoritative-move-runner`
  - `data-driven-runtime-actions`
- Related active changes:
  - `add-effect-application-layer`
  - `refactor-authoritative-action-pipeline-foundation`
- Affected code during apply:
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/RuntimeEffects/*`
  - `Shared/DG.GameCore/Rules/Commit/CommitRules.cs`
  - `Shared/DG.GameCore/Rules/Actions/*`
  - `Server/Hotfix/AuthoritativeMove/Infrastructure/DebugWorldEditService.cs`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Networking/Runtime/ClientMoveNetworkSubmitter.cs`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Debug/Runtime/ClientWorldDebugEditor.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/*`
  - `Server/Tests/AuthoritativeMoveVerification/*`

## Verification
- Proposal validation:
  - `openspec validate refactor-effect-boundary-closure --strict --no-interactive`
- Apply-stage automated validation:
  - `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
  - `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
  - Unity TestFramework EditMode tests for commit-only effect write, source-based tag/component contribution, local client effect isolation, and reset runtime sources.
- 用户手动端到端验证：
  - Play Mode 连接服务端权威路径。
  - 启动两个客户端，A 通过调试工具施加和移除 temporary pushable、immobile、auto move、port、tag effect。
  - B 只通过服务端 WorldDelta 观察 final Component/tag 变化。
  - 全部 runtime effect 移除后，A 与 B 都回到对象静态配置决定的 final state。
