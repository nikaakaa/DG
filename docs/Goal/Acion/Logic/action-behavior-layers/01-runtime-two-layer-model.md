# 运行时两层模型

## 结论

行为层最低限度保留两层:

```text
BehaviorEngine
  全局每 tick 编排器 + 运行中实例集合

IBehaviorRunner
  单个 ActionBehaviorInstance 的状态机
```

这不是为了追求类数量少,而是为了把两种完全不同的复杂度分开:

```text
全局一致性复杂度   admission / arbitration / apply / fact output / running instance index
局部行为状态复杂度 phase / timer / contact data / payload / snapshot
```

## BehaviorEngine 职责

`BehaviorEngine` 负责每 tick 的全局顺序:

```text
drain inputs
resolve action spec
build admission candidates
read existing claims
arbitrate conflicts
start accepted instances
step due running instances
collect output
apply CommitProposal
emit facts / events
release completed or interrupted claims
```

它可以读取 world snapshot,但不能把具体行为逻辑写进自己:

```text
禁止:
  if (runnerId == "rotate") ...
  switch (behaviorId) ...
  if (spec.SpecId == "charge_attack") ...

允许:
  registry.Create(runnerId)
  runner.Enter(ctx)
  runner.Tick(ctx)
```

## IBehaviorRunner 职责

`IBehaviorRunner` 只负责单个 instance 的内部状态和输出意图:

```text
Enter(ctx)
Tick(ctx)
Exit(ctx)
Snapshot()
Restore(snapshot)
```

Runner 可以读 `ctx.World`,但不能直接写 world。它的输出只能是:

```text
Claim
CommitProposal
BehaviorEvent
DeferredAction(少数场景)
trace / reason
```

## 为什么不能压成一层

如果把 Runner 状态塞回 `BehaviorEngine`,会出现:

```text
BehaviorEngine:
  rotatePhase
  rotateContactUntilTick
  doorPhase
  chargePhase
  parryWindow
  ...
```

这会让主流程重新认识具体行为,直接破坏 DoD。

如果让每个 Runner 自己处理 admission / apply / world mutation,会出现:

```text
MoveRunner 自己处理格子冲突
RotateRunner 自己处理长期占用
DoorRunner 自己写 world
ChargeRunner 自己仲裁目标
```

全局一致性被分散到所有 Runner,网络重放、服务端权威和冲突原因都会变得不可控。

## 为什么不要硬写三层

`BehaviorRuntime` 和 `BehaviorInstanceRunner` 可以作为代码内部拆分存在,但不是架构必需层。

真正不可省的是职责分离:

```text
per-tick orchestration   本 tick 编排与结果应用
cross-tick instances     运行中 instance 与 claim 生命周期
```

这两件事可以放在一个 `BehaviorEngine` 类里,也可以内部拆成两个协作类。文档不应把当前 WIP 的类名拔高成最终架构。

## 保留口径

```text
必要:
  BehaviorEngine
  IBehaviorRunner
  per-instance Runner
  CommitProposal
  Claim 最小集合

可延后:
  BehaviorRuntime / BehaviorInstanceRunner 作为硬分层
  ClaimChannel
  DeferredAction 核心化
  复杂 Snapshot 框架
```
