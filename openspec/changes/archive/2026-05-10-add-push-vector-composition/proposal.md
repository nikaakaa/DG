# Change: 同 tick 推力向量合成仲裁

## Why

当前 deferred push 已经能跨 tick 传播，也已经能合并同 tick 等价输出，但同一 subject 在同一 tick 收到不同方向推动时仍会保留多条方向相反或方向不同的 push action，导致闭环结构产生上下或左右震荡。

本次变更补一个明确的 push 仲裁层：同 tick 汇入同一 subject 的推动先合成为一个离散向量，再按格子路径逐步尝试移动，避免把向量结果当成飞行位移直接穿过碰撞。

## What Changes

- 新增同 tick push vector composition 规则，把同一 ready tick、同一 subject 的多个 push contribution 聚合为一个净推动意图。
- 相反方向 contribution 先在向量中抵消；同向 contribution 累加到 strength/contribution 元信息。
- 所有 push contribution 默认进入同一个合成池，不额外引入 action spec group 复杂度。
- 合成结果可能是零向量、单轴向量或双轴向量；双轴结果使用确定性单格路径拆分，非零结果必须按离散单格路径验证碰撞，不能直接跳到向量终点。
- 输出保留 contribution count、per-direction contribution 和预留 energy metadata；同方向多股推力会作为未来 strength 输入记录，但本次不实现能量衰减、冲量持续或强度推动多格。
- 不改变普通 movement claim、target claim、commit resolver 的既有职责。

## Non-Goals

- 不实现连续物理、速度、惯性或真实力学。
- 不实现能量衰减、最大传播步数、蓄力、多格强推或质量系统。
- 不让客户端预测或本地计算 push 仲裁。
- 不修改 DOTween/客户端表现层。
- 不引入同 tick 全链求解；跨 tick 传播仍然按 deferred action 进行。
- 不执行 Unity Player build。

## Impact

- Affected specs:
  - `data-driven-runtime-actions`
  - `claim-driven-action-arbitration`
  - `atomic-action-behavior-layer`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Commit/CommitRules.cs`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor/ClientWorld/`
  - server verification tests under `Server/Tests/AuthoritativeMoveVerification/`
