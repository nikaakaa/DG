# 行为层工作流

## 普通玩家输入

```text
Unity Input System
  -> Client InputIntent
  -> C2G request
  -> Fantasy Handler
  -> server validation
  -> server InputIntentBuffer
  -> IntentAdapter
  -> player action request
  -> action pipeline
  -> WorldDelta
  -> Unity mirror + presentation
```

关键规则：

- 客户端只提交意图。
- 服务端按 session、actor 控制权、输入窗口和去重规则承认输入。
- 授权成功不等于 action 成功。
- 最终坐标只来自服务端 WorldDelta。

## Debug / AI / Mechanism 输入

```text
Debug tool / AI / Mechanism
  -> WorldAction
  -> WorldActionQueue
  -> ready tick drain
  -> ActionRequest
  -> action pipeline
```

Debug 可以绕过普通玩家输入授权，但不能绕过权威行为 pipeline。

AI 和机关也不应该直接写坐标。它们输出 action input，由同一套规则裁决。

## 一次 immediate action

适合普通 move / spawn / remove / apply runtime effect。

```text
ready ActionRequest
  -> lookup ActionSpec
  -> targeting
  -> gating
  -> subject
  -> strategy
  -> claim
  -> arbitration
  -> plan / commit proposal
  -> commit
  -> ActionResult
  -> ActionFact or fallback PresentationFact
  -> WorldDelta
```

工作流约束：

- action 在当前 tick 完成。
- 不进入 active store。
- 不产生跨 tick reservation。
- 如果没有显式 ActionFact，composer 可以按通用 fallback 生成普通表现事实。

## 一次 active action

适合 rotate、charge、门、等待条件行为。

```text
ready ActionRequest
  -> behavior accepts
  -> create ActionBehaviorInstance
  -> acquire ReservationScope
  -> emit start ActionFact
  -> store active instance
```

后续 tick：

```text
active store tick
  -> state machine OnTick
  -> release scheduled outputs
  -> maybe emit ActionFact
  -> maybe emit DeferredAction
  -> maybe commit completion output
  -> release reservation at end
```

工作流约束：

- active 行为期间命中 subject lock 的 incoming action 默认 reject。
- 命中 cell/resource reservation 的 action 必须按 reservation policy 裁决。
- completion 时才释放最终 result 或 commit。
- 客户端只能按服务端事实播放。

## Deferred output

deferred output 是未来 action input。

```text
current action
  -> DeferredAction
  -> WorldActionQueue.EnqueueDeferred
  -> ready tick
  -> new ActionRequest
```

关键规则：

- 当前 action 不等待下游结果。
- deferred action 必须保留 causality / origin context。
- dedupe / merge / contribution 仍走普通队列语义。
- rotate impact push、blocked handoff push 和普通 push 本质同源。

## Push 工作流

普通 push 的目标语义：

```text
push request
  -> direction target cell
  -> subject selection
  -> blocked policy
  -> if blocked and derive enabled
       emit DeferredAction push
  -> if movable
       commit source or subject move
```

同 tick 多 push：

```text
same subject + same ready tick
  -> contribution merge
  -> one final push vector / outcome
```

闭环和持续输出：

```text
tick N action
  -> finite result
  -> deferred output for tick N + cost
tick N + cost
  -> re-enter pipeline with new world facts
```

禁止：

```text
在同一 tick 内递归展开 push chain 到终点
让 parent action 等 child push 成功
用 pending chain 表达普通 push 传播
```

## Rotate 工作流

rotate 是 push 管线里的 special subject response。

触发前提：

```text
push 类 action
direction_cell target
allows connected body subject
blocked policy 有 DeriveAction
命中 connected body
connected body 有 RotatePivotComponent
```

仲裁：

```text
同 ready tick + 同 body
  -> 按 body 分组
  -> 统计 pivot
  -> 根据 push contribution 计算 torque
```

成功：

```text
StartTick
  -> emit RotatePivotGroup Success
  -> lock body members
  -> no coord commit
EndTick
  -> commit final coords/directions
  -> unlock
```

碰撞：

```text
StartTick
  -> emit RotatePivotGroup Bounce
  -> lock body members
  -> reserve authoritative coords
ContactTick
  -> emit RotatePivotImpact
  -> release delayed push
EndTick
  -> no rotate coord commit
  -> unlock
```

当前待完善工作流：

```text
CostTicks = N
  -> 90 度被切成 N 个 tick 弧段
  -> 每个 tick 弧段做 union(member sweep)
  -> 第一个有 contact 的 tick 收集该 tick 内全部 blockers
  -> bounce return ticks = out ticks
```

## RuntimeEffect 工作流

```text
Action / debug / mechanism
  -> EffectApplication
  -> RuntimeEffectStore
  -> lifecycle tick
  -> source contributions
  -> ComponentStateResolver / tag settlement
  -> final world facts
  -> behavior pipeline queries
```

约束：

- runtime effect 不直接绕过 action/commit 修改权威行为结果。
- final component/tag fact 是 targeting、gating、condition 和 subject policy 的输入。
- 移除 runtime effect 只移除对应 source。

## Presentation 工作流

服务端：

```text
ActionFact
  -> PresentationFact projection
  -> WorldDelta
  -> Fantasy broadcast
```

客户端：

```text
WorldDelta
  -> apply snapshot / changed entities to ClientMapWorld
  -> translate PresentationFact
  -> build BehaviorPlaybackPlan
  -> schedule track
  -> visual update
```

group 播放：

```text
RotatePivotGroup
  -> RigidBodyGroupTrack
  -> entity ownership
  -> connection ownership
  -> visual pose interpolation
  -> final authoritative alignment
```

约束：

- 播放期间普通 entity animation 不能覆盖 group member。
- 连接线要跟随 group pose。
- 播放结束后对齐服务端 snapshot。
- 播放状态不写回 `ClientMapWorld` 权威镜像。

## 新增普通行为工作流

1. 先判断能否只改 Luban `ActionSpec` 数据。
2. 如果现有 selector / subject / policy / commit 能表达，只补配置和测试。
3. 如果需要新底层能力，新增 `IActionStrategy`。
4. 如果需要新 target，新增 targeting selector。
5. 如果需要新遇阻结果，新增 blocked outcome handler。
6. 如果需要新世界修改，新增 commit proposal kind 和 handler。
7. 如果行为跨 tick，新增 active behavior instance / state machine 语义。
8. 如果客户端需要特殊表现，新增 ActionFact 投影和 playback plan。
9. 补 Unity TestFramework EditMode。
10. 补 server verification。
11. 用户手动跑服务端 + Play Mode + 双客户端端到端验证。

## OpenSpec 工作流

需要 OpenSpec proposal 的情况：

```text
新增能力
改变架构
改变行为语义
改变协议或配置模型
改变性能/安全边界
```

文档整理、错字、代码注释、已实现行为说明不需要 proposal。

OpenSpec 实施顺序：

```text
read openspec/project.md
read related specs
check openspec list
create change proposal
validate --strict
approval
implementation
tests
manual validation
archive after user says tested
```

用户说某个 OpenSpec 可以 archive，表示用户已经测试过，可以直接归档。

## 验证工作流

行为层自动验证优先顺序：

```text
Unity TestFramework EditMode
dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore
dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore
openspec validate <change-id> --strict --no-interactive
```

端到端验证必须由用户手动执行：

```text
启动 Fantasy 服务端
启动 Unity Play Mode
必要时启动两个客户端
执行具体场景
观察 WorldDelta、最终坐标、表现事实和视觉同步
```
