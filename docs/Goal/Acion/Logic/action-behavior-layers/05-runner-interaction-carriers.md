# Runner 交互载体

## 结论

Runner 之间不能直接通信。所有跨行为影响必须经过封闭载体。

常规载体:

```text
World Fact
Claim
CommitProposal
BehaviorEvent
```

少数场景工具:

```text
DeferredAction
```

## World Fact

World Fact 是 Runner 从 world 读取到的事实:

```text
Position
Component
Tag
RuntimeEffect 结算结果
MovementPermission
Blocking / Collider / Pushable
```

合法例子:

```text
cast_freeze apply RuntimeEffect
ComponentStateResolver 结算出 MovementPermission(canMove=false)
MoveRunner admission 读 MovementPermission 后自拒
```

这不是 Runner 互调。它是通过 world 间接通信。

## Claim

Claim 表达同 tick 或跨 tick 的竞争意图:

```text
SubjectClaim(entity, Exclusive)
CellClaim(coord, Exclusive)
```

Claim 给仲裁器看,不是给客户端看,也不直接写 world。

## CommitProposal

CommitProposal 表达获胜后的世界写指令:

```text
MoveEntity
AddRuntimeEffect
RemoveTag
```

它必须通过 CommitResolver 应用。

## BehaviorEvent

BehaviorEvent 表达行为时间线事件:

```text
charge_started
charge_released
rotate_contacted
door_opened
dummy_emitted
```

它用于:

```text
表现层
网络事件
审计 trace
调试可视化
```

它不应该驱动服务端 world 变更。服务端 world 变更必须来自 CommitProposal。

## DeferredAction

DeferredAction 只用于触发另一个完整 action,并要求它重新经过 admission。

适合:

```text
rotate 接触后下 tick 触发 push
trap 触发一个完整 damage action
chain reaction 需要下一轮仲裁
```

不适合:

```text
打开门移除 blocking tag
施加 freeze effect
播放动画
写一个普通 world delta
```

这些直接用 CommitProposal 或 BehaviorEvent。

## 反例

错误:

```text
ChargeRunner 直接 new MoveRunner 调 Tick
DoorRunner 订阅 RotateRunner.OnComplete
MoveRunner if (RotateRunner.IsRunning(entity)) return reject
```

正确:

```text
RotateRunner 持有 Claim
MoveRunner admission 看到 claim 冲突而 reject
```

## 判断口诀

```text
读状态     -> World Fact
抢资源     -> Claim
改 world   -> CommitProposal
通知表现   -> BehaviorEvent
触发 action -> DeferredAction(少数场景)
```
