# RuntimeEffect 状态层

## 结论

RuntimeEffect 是持续状态层。freeze / immobile / burning / shield / haste 这类"形容词状态"优先走 RuntimeEffect,不应新建专属 Runner。

```text
行为层 Behavior:
  cast_freeze / rotate / open_door / charge_attack

状态层 RuntimeEffect:
  immobile / burning / shield / temporary_blocking / temporary_tag

事实层 World Fact:
  MovementPermission / Tag / Component / Position
```

## 当前源码状态

当前源码已经有 RuntimeEffect 的骨架和主要链路:

```text
EffectSpec
EffectApplication
RuntimeEffectStore
RuntimeEffectInstance
ComponentStateResolver
ApplyEffectRunner
RemoveEffectRunner
AddRuntimeEffectCommitHandler
RemoveRuntimeEffectCommitHandler
```

这说明状态层方向已经有落点。但这不等于所有端到端场景已经完成。

## cast_freeze 应如何落地

freeze 不是一个值得新建专属 `FreezeRunner` 的行为。更准确地说:

```text
cast_freeze 是行为
immobile/frozen 是状态
MovementPermission(canMove=false) 是事实
```

推荐链路:

```text
ActionSpec: cast_freeze
  primitive / runner: apply_effect_runner
  effectSpecId: temporary_immobile 或 freeze_effect

ApplyEffectRunner
  -> CommitProposal.AddRuntimeEffect

CommitResolver
  -> RuntimeEffectStore.Add

ComponentStateResolver
  -> MovementPermission(canMove=false, canBePushed=false)

Move admission / planning
  -> 读取 MovementPermission
  -> 拒绝移动
```

Tag 也可以表达状态,但"不能移动"已经有 `MovementPermissionComponent` 这条更明确的通道。不要默认把 freeze 写成 tag gating。

## RuntimeEffect 不该做什么

RuntimeEffect 是被动状态,不主动触发行为。

不该:

```text
effect 到期时主动调用 Runner
effect tick 时直接 move entity
effect 自己发网络消息
```

应该:

```text
生命周期系统发现过期
RemoveRuntimeEffect proposal 或 world effect store 过期
ComponentStateResolver 重新结算事实
其他 Runner 在 admission 时读取新事实
```

## 需要测试证明的内容

RuntimeEffect 文档只能说"已有骨架",不能说"完整完成",除非测试覆盖:

```text
1. AddRuntimeEffect 走 CommitProposal,不绕过 world
2. effect 按 tick 过期
3. temporary_immobile 结算出 MovementPermission(canMove=false)
4. move/push admission 读取 MovementPermission 并拒绝
5. effect 过期后 MovementPermission 消失或恢复默认
6. 客户端同步/表现能看见最终状态
```

## 什么时候才需要新 Runner

需要新 Runner:

```text
charge:
  Anticipating -> Charging -> Released

door:
  Closed -> Opening -> Open -> Closing

rotate:
  Anticipating -> Contacting -> Recovering -> Completing
```

不需要新 Runner:

```text
freeze:
  施加一个持续状态

shield:
  施加一个持续状态

burning:
  如果只是持续状态和数值修正,走 effect
  如果每 tick 主动产生 damage action,再考虑 DeferredAction 或专门行为
```
