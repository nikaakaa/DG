# Rules 接入子目标

## 定位

本子文档负责定义 Ability/Effect 如何接入服务端权威 rules 主路。

它不重新设计移动规则，而是把运行时能力和效果接到当前已存在的 state-driven action / intent / plan / commit 管线。

## 当前主路

当前规则主路保持不变：

```text
WorldAction
  -> BehaviorIntent
  -> IntentArbiter
  -> RulePlanner
  -> ConflictResolver
  -> Commit
```

Ability/Effect 接入后仍然走这条主路。

## 新增 action 类型

Effect 相关 action：

```text
ApplyRuntimeEffect
RefreshRuntimeEffect
RemoveRuntimeEffect
ExpireRuntimeEffect
```

Ability 相关 action：

```text
ActivateAbility
CancelAbility
GrantAbility
RemoveAbility
```

Debug action 必须进入同一队列，不能直接改 world。

## Tick 阶段

服务端 tick 阶段方向：

```text
1. Drain ready actions
2. Detect expired runtime effects
3. Process ability activation actions
4. Process runtime effect actions
5. Build behavior intents
6. Arbitrate intents
7. Create plans
8. Resolve conflicts
9. Commit movement/world changes
10. Commit effect changes
11. Flush dirty state and WorldDelta
```

子文档后续需要细化 move commit 与 effect commit 的同 tick 排序。

## EffectCommitProposal

Effect commit proposal 表达待提交效果变化。

类型：

```text
ApplyEffect
RefreshEffect
RemoveEffect
ExpireEffect
GrantAbility
RemoveGrantedAbility
```

proposal 必须包含：

```text
source action id
source entity id
target entity id
effect kind
effect id
server tick
context
payload
```

## 与 IntentArbiter 的关系

Effect 不替代 `IntentArbiter`。

Effect 通过贡献 tag/component 影响仲裁：

```text
RuntimeEffectInstance
  -> WorldTag.BlockPlayerMove
  -> IntentArbiter reads blocked tags
```

`IntentArbiter` 不需要理解每个 EffectKind。它只读取最终 tag/component 状态。

## 同 tick 顺序

需要保证：

```text
同 tick 多个 effect action 顺序稳定
同 tick effect refresh/remove 不产生随机结果
同 tick move 与 effect 互相影响规则明确
```

排序依据：

```text
server tick
action priority
source action id
effect id
entity id
```

具体排序规则放入实现设计前必须写清。

## 失败语义

Rules 接入层要能区分：

```text
action 不合法
ability 激活失败
effect definition 缺失
target 缺失
stack policy 拒绝
commit 冲突
effect 已过期
```

失败结果要能进入测试和 Debug 观察。

## 与其他子文档的关系

```text
runtime-ability-model.md
  提供 AbilityActivationRequest。

runtime-effect-model.md
  提供 RuntimeEffectSpec / RuntimeEffectInstance。

runtime-effect-world-delta.md
  接收 commit 后 dirty changes。

runtime-effect-verification.md
  验证 tick 阶段、commit 顺序和失败原因。
```
