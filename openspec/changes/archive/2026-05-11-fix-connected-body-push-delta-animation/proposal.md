# Change: 修复连接体推动的权威 delta 与整体动画表现

## Why
当前推动链路在“玩家或机关触发推动连接体”时，客户端表现可能只显示受力成员或第一个受力位置，而不是按服务端权威结果表现所有相关成员。已排查到客户端动画层按服务端 `WorldDelta` 和 animation metadata 逐个生成动画，因此该问题不应通过客户端猜测连接体成员来修补，而应保证服务端权威推动链路输出完整的移动 delta 与推力反馈 metadata。

## What Changes
- 修正推动 handoff / deferred push 路径，确保 downstream subject 为连接体时保留全体 subject members。
- 确保连接体推动成功后，服务端 `WorldDelta.ChangedEntities` 包含每个实际移动成员。
- 确保连接体推动成功后，服务端 animation metadata 覆盖每个实际移动成员，使客户端表现层能为每个成员生成动画。
- 明确普通 push 链的 source-stops-at-handoff 语义：中间连接体只继续传递 push 并播放 push feedback，不会因为下游让路而 parent retry 或补一次移动。
- 允许 metadata-only `WorldDelta` 同步给客户端，使“正在推但没有移动”的连接体成员也能播放 feedback 动画。
- 调整客户端动画测试边界：客户端不得根据本地连接体关系补齐权威 delta，但必须对服务端发来的每个连接体成员 delta 逐个播放动画。
- 补充服务端验证、Unity TestFramework EditMode 测试和手动 Play Mode / 双客户端验证步骤。

## Impact
- Affected specs: `claim-driven-action-arbitration`, `authoritative-move-runner`, `client-world-runner`
- Affected code: `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`, `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`, `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`, `Shared/DG.GameCore/Rules/Commit/ConflictResolver.cs`, `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`, `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`, `Client/DG_Client/Assets/Scripts/ClientWorld/Networking/Runtime/ClientMoveNetworkRuntime.cs`, `Client/DG_Client/Assets/Scripts/ClientWorld/View/ClientAnimationLayer.cs`, `Client/DG_Client/Assets/Scripts/ClientWorld/View/ClientWorldVisuals.cs`, `Client/DG_Client/Assets/Tests/Editor/ClientWorld/*`
