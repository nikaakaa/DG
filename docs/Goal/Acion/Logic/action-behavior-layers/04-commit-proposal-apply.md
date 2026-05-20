# CommitProposal / Apply

## 结论

CommitProposal 是世界写指令。它表达"已经通过 admission 的结果要如何修改 world"。

CommitProposal 不是:

```text
不是 Claim
不是锁
不是长期占用
不是动画事件
不是 runner 生命周期事件
不是空占位
```

## CommitProposal 的不变量

```text
atomic       一次 proposal 表示一次原子世界变更
sealed       kind 是世界能被修改的方式全集,不是行为全集
auditable    apply 成功/失败要有结果
deterministic 同输入同 world 必须得到同结果
```

## CommitProposalKind 是封闭原语

允许的 kind 应该非常少:

```text
MoveEntity
SetDirection
CreateEntity
DeleteEntity
AddTag
RemoveTag
AddRuntimeEffect
RemoveRuntimeEffect
SetComponentResult
SetAutoMoveTick
ClearRuntimeSources
```

不要因为新行为加新的 kind:

```text
错误:
  ChargeAttack
  FreezeTarget
  OpenDoor
  RotatePivot

正确:
  ChargeAttack = MoveEntity + SetDirection + BehaviorEvent
  FreezeTarget = AddRuntimeEffect
  OpenDoor = RemoveTag(blocking) 或 AddRuntimeEffect(door_open)
  RotatePivot = 多个 MoveEntity + BehaviorEvent
```

## CommitResolver

CommitResolver 是唯一写 world 的地方。

Runner 输出:

```text
CommitProposal.AddRuntimeEffect(...)
CommitProposal.MoveEntity(...)
```

Engine 收集并交给 resolver:

```text
CommitResolver.Resolve(world, proposals)
```

Runner 禁止直接:

```text
ctx.World.MoveEntity(...)
ctx.World.AddTag(...)
ctx.World.RemoveComponent(...)
```

## commit 失败的含义

commit 失败应该是最后防线,不是常规仲裁方式。

常规冲突:

```text
Claim 冲突 -> admission reject
```

commit 失败:

```text
world 已变化导致 proposal 不再合法
proposal 参数非法
目标 entity 不存在
commit handler 拒绝
```

如果大量业务冲突靠 commit fail 解决,说明 admission/claim 过弱。

## CommitProposal 与 Fact

世界 delta fact 应该由成功 commit 自动投影:

```text
MoveEntity accepted -> WorldDeltaFact.EntityMoved
AddRuntimeEffect accepted -> WorldDeltaFact.RuntimeEffectApplied
RemoveTag accepted -> WorldDeltaFact.TagRemoved
```

Runner 不需要重复 emit 这些世界 delta。

Runner 自己 emit 的是行为时间线:

```text
BehaviorEvent("charge_started")
BehaviorEvent("rotate_contacted")
BehaviorEvent("door_opened")
```
