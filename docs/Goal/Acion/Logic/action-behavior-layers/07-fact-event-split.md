# Fact / Event 拆分

## 结论

事实输出拆成两类:

```text
WorldDeltaFact   世界变更事实,从 CommitProposal 自动投影
BehaviorEvent    行为时间线事件,由 Runner 按需 emit
```

不要把世界 delta 和具体行为事件混在一个不断膨胀的 enum 里。

## WorldDeltaFact

WorldDeltaFact 是世界状态变化的事件化结果。

来源:

```text
CommitProposal accepted
```

例子:

```text
EntityMoved
EntitySpawned
EntityRemoved
RuntimeEffectApplied
RuntimeEffectRemoved
TagAdded
TagRemoved
ComponentChanged
```

特征:

```text
sealed
少量
跟 CommitProposalKind 基本 1:1
客户端可用来同步世界状态
服务端可审计
```

## BehaviorEvent

BehaviorEvent 是行为自己的时间线事件。

来源:

```text
IBehaviorRunner output
```

例子:

```text
charge_started
charge_released
rotate_started
rotate_contacted
rotate_completed
door_opened
dummy_emitted
```

特征:

```text
open kindId
payload 由行为定义
表现层按 kindId 分派
主流程不认识具体 kindId
未注册 kindId 可以忽略或仅 trace
```

## 为什么要拆

如果不拆:

```text
ActionFactType:
  EntityMoved
  RuntimeEffectApplied
  RotateStarted
  RotateContacted
  ChargeStarted
  ChargeReleased
  DoorOpened
  ParryWindowOpened
  ...
```

加新行为会修改主流程 fact 文件,DoD 失效。

拆分后:

```text
世界怎么变:
  sealed WorldDeltaFact

行为发生了什么:
  open BehaviorEvent(kindId, payload)
```

## 归类例子

```text
WorldDeltaFact:
  EntityMoved
  RuntimeEffectApplied
  TagRemoved
  EntitySpawned

BehaviorEvent:
  ChargeStarted
  DoorOpened
  RotateContacted
```

## 落地节奏

顶层 rotate 垂直切片可以先沿用现有 fact,但一旦 PR2/PR3 继续出现 behavior-specific fact 膨胀,就提前拆。

不要等到所有行为都迁完才拆,那时迁移成本会更高。
