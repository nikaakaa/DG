# Change: 解耦权威表现事件

## Why
当前 `WorldDeltaAnimationMetadata` 同时承载状态变化辅助信息和动画表现语义，服务端 `AuthoritativeWorldTickRunner` 通过动作名推断 `WorldDeltaMotionKind` 与 `styleKey`，导致新增表现需要修改权威 tick、协议枚举、客户端 motion 映射和具体动画分支。

本变更将表现语义抽为可扩展 `PresentationEvent`，保留服务端权威 `WorldDelta` 同步模型，不实现客户端预测、逻辑回滚或独立表现通知通道。

## What Changes
- 新增服务端权威 `PresentationEventComposer` 边界，在规则执行后、同步发送前生成表现事件。
- 将 `WorldDelta` 内的动画元数据语义升级为 `PresentationEvent`，继续随 `G2C_WorldDeltaNotify` 同 tick 发送。
- 使用字符串 `kindId` 和 `cueId` 表达表现事件语义和表现入口，不新增表现枚举。
- 增加表现专属 `payloadJson`，只承载通用字段无法表达的表现参数。
- 新增配置边界，将动作或执行结果映射到表现事件 kind/cue，而不是在权威 tick 中比较普通 action 名称。
- 客户端表现层消费 `PresentationEvent`，世界状态仍只由 `WorldDelta` 的实体快照和删除列表决定。

## Non-Goals
- 不实现客户端预测、逻辑回滚、状态历史或重放。
- 不拆分独立 `G2C_PresentationEventNotify`。
- 不让表现事件影响权威世界状态。
- 不修改 Unity `.meta` 文件，不手改生成协议文件。

## Impact
- Affected specs:
  - `authoritative-move-runner`
  - `client-world-runner`
  - `data-driven-runtime-actions`
- Affected code:
  - `Shared/DG.GameCore/World/Snapshots`
  - `Shared/DG.GameCore/Configuration`
  - `Server/Hotfix/AuthoritativeMove/Runtime`
  - `Server/Hotfix/AuthoritativeMove/Sync`
  - `Tools/NetworkProtocol/Outer/OuterMessage.proto`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Networking/Runtime`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Presentation`
  - `Client/DG_Client/Assets/Tests/Editor`
- Verification:
  - `openspec validate refactor-presentation-events --strict --no-interactive`
  - `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
  - `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
  - Unity TestFramework EditMode tests
  - 用户手动 Play Mode 端到端验证服务端权威同步和双客户端表现一致
