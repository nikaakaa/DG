# Change: 抽象统一输入意图层

## Why

当前普通玩家输入已经具备服务端拍点缓冲和 Direction-only 移动提交能力，但输入语义仍贴近移动请求。后续需要 Unity Input System、AI、Replay、Debug、Script、RuntimeEffect 控制权、AOI 和非移动意图时，如果继续沿用方向输入入口，会让输入、授权、玩法裁决和同步边界混在一起。

本变更将输入收口为统一 `InputIntent`：输入层只表达输入事实，后续适配层再把意图转为 `ActionRequest` / `WorldAction`。

## What Changes

- 新增统一输入意图模型边界：`InputIntent`、`InputKind`、`InputSourceKind`、`TargetHint`、`RhythmJudge`。
- 客户端正式普通输入入口迁移到 Unity Input System，不再以 `Input.GetKeyDown` / `KeyCode` 作为正式输入路径。
- 客户端可生成声明型 `InputIntent`，服务端验证后承认为服务端输入事实。
- 服务端普通输入缓冲从 Direction-only 语义升级为 `InputIntent` 缓冲；第一阶段普通方向 `Move` 同 actor + 同服务端消费窗口 + 同输入通道保留最后一个。
- 服务端输入缓冲拆分客户端声明 `BeatTick` 和服务端消费窗口 tick；客户端快速连续请求不得自动滚入多个未来 tick。
- 新增 `IntentAuthorization` 边界：授权只判断来源、actor、权限、限流、过期、重复，不裁决碰撞、推动、机关、冷却或最终坐标。
- 新增 `IntentAdapter` 边界：把已授权、已消费的 `InputIntent` 转为现有 action pipeline 可消费的 `ActionRequest` / `WorldAction`。
- 保留现有 `WorldDelta` 作为最终权威同步结果；输入响应不替代 `WorldDelta`。

## Impact

- Affected specs:
  - `client-world-runner`
  - `authoritative-move-runner`
  - `data-driven-runtime-actions`
- Affected code:
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Input`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Networking/Runtime`
  - `Server/Hotfix/AuthoritativeMove/Handlers`
  - `Server/Hotfix/AuthoritativeMove/Runtime`
  - `Shared/DG.GameCore/ActionRuntime`
  - `Shared/DG.GameCore/Domain/Components/PlayerControlComponent.cs`
  - protocol files under `Tools/NetworkProtocol/Outer`
- Validation:
  - Unity TestFramework EditMode for client input intent model, buffer, pending state and mirror boundary.
  - Server verification project for input intent buffer, authorization and move adapter.
  - Manual end-to-end validation with server and two Unity clients.
