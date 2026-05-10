# Change: 合并同 tick 等价 deferred push

## Why

当前 deferred push 已经能表达跨 tick 推力传播、分叉和闭环振荡，但手动测试发现多个传播分支在同一 tick 汇合到同一 subject 时，会重复入队大量等价 `player_push` action。它们只因 parent `causality` 不同而拥有不同 `dedupeKey`，导致 `WorldActionQueue` 膨胀、服务器日志刷屏，并可能拖慢权威 tick。

这不是 push 打断仲裁问题，也不是同 tick 全局求解问题。本次只补最小保护：同一 ready tick、同一行为、同一方向、同一 resolved subject 的 deferred push 只入队一次。

## What Changes

- 为 deferred output 定义不依赖 parent causality 的等价 key。
- 在 deferred output 回到 `WorldActionQueue` 前合并同 tick 等价 deferred。
- 保留不同方向、不同 subject、不同 ready tick 的 deferred output。
- 保留现有分叉传播和闭环周期行为，只阻止同质重复 action 膨胀。
- 增加日志摘要，显示本 tick deferred 入队数量、合并数量和被合并的等价 key。

## Non-Goals

- 不实现 push 打断仲裁层。
- 不实现相反方向推力抵消。
- 不实现 wave TTL、能量、衰减、最大跳数或强度累加。
- 不改变 `ActionSpec` cost 配置语义。
- 不引入同 tick 全链求解或全局事务求解。
- 不执行 Unity Player build。

## Impact

- Affected specs:
  - `data-driven-runtime-actions`
  - `atomic-action-behavior-layer`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`
  - `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
  - `Shared/DG.GameCore/Testing/SandboxScenario.cs`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor/ClientWorld/`
  - server verification tests under `Server/Tests/AuthoritativeMoveVerification/`
