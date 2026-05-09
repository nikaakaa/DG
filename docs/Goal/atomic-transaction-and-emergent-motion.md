# 原子事务与涌现运动

## 文档目的

这份文档确定 DG 规则层的一个核心边界：

```text
一次 action / 一次 tick 内的事务必须是有限、原子、可提交的。
持续运动、闭环装置、推力循环等长期行为必须通过多个 tick 的重复原子事务涌现出来。
```

它不设计完整打断系统，也不处理所有优先级细节。这里先假设同一 tick 内如果多个指令命中同一 subject，要么合并，要么忽略重复，不能让同一 subject 在同一事务里反复消费。

## 核心结论

无限行为不应该来自单次 pending push 链的无限展开。

正确模型是：

```text
单次事务有限
系统演化可无限
```

也就是：

```text
tick N
  收集当前可执行 action
  解析 subject
  合并同 tick 输入
  每个 subject 最多消费一次
  生成有限 commit
  提交 world delta

tick N + cost
  根据新世界状态再次收集 action
  再提交下一次有限变化
```

如果一个装置看起来无限运行，它应该是很多次有限事务连续发生，而不是一次 action 内部一直递归。

## 原子事务边界

一次原子事务应满足：

```text
输入有限
subject 集合有限
每个 subject 在同一 tick 内最多消费一次
commit proposal 有限
world delta 有限
事务结束后必须完成、失败、取消或产出未来 tick 的显式 action input
```

一次原子事务不应该：

```text
在同一 tick 内反复消费同一个 subject
在 pending chain 里无限递归
为了求完整链条而展开整个图
把闭环反馈作为同一事务内的新输入继续结算
让一个 entity 同时属于两个不同 action unit 的提交所有权
```

## 同 tick push 叠加

同一 tick 内多个 push 可以叠加，但必须有消费边界：

```text
同一 subject 同 tick 只消费一次叠加后的 push
消费后产生的 downstream push 最早在 tick + cost 生效
```

这允许多个输入合力：

```text
push A -> X
push B -> X
push C -> X

X 本 tick 接收 strength = A + B + C
X 本 tick 只处理一次
```

它不允许同 tick 反馈放大：

```text
X 输出 push
push 同 tick 回到 X
X 再消费
X 再输出
...
```

如果允许这种反馈，闭环结构会变成同 tick 无限增幅器，事务无法收敛。

## 闭环装置的语义

闭环装置不应该被 pending push 链强行求解到终点。

正确语义是：

```text
外部输入触发闭环
闭环本 tick 消费一次输入
闭环可以产生输出 push
输出 push 进入下一次 tick / 下一次 cost
如果下一 tick 条件仍成立，继续输出
```

因此闭环的“无限”表现为：

```text
tick 0       输入触发
tick a       输出一次 push
tick 2a      再输出一次 push
tick 3a      再输出一次 push
...
```

周期由 push cost 决定：

```text
period = a
```

这不是一次事务里的无限链，而是世界规则反复产生原子动作。

## 涌动增幅 push 机器示例

这个例子用于约束“闭环持续输出”和“未来 push strength 模型”的边界。

可以抽象成：

```text
外部输入 A
  ↓
入口 subject L
  → 环路内部若干 connected body / port
  ↘ feedback 回到 L
  ↘ output 口 O 指向外部 subject T
```

假设每次 push 的 cost 为 `a`。

第一阶段没有正式 strength 数据模型时，它不应该在一次 tick 内无限展开，也不应该把 feedback 立刻重新喂回 `L`：

```text
tick 0
  A push L
  L 本 tick 消费一次
  feedback 回到 L => 被同 tick subject 消费边界截断
  output O 有有效外部 target => 生成 deferred output，ready tick = 0 + a
  L 本体不因为反馈到自己而移动

tick a
  deferred output 重新进入 action 管线
  如果世界状态仍让 O 有有效输出 => 再生成下一次 deferred output，ready tick = a + a

tick 2a
  重复
```

所以第一阶段它是周期输出装置：

```text
period = a
strength = 不增长
```

如果 output 口没有有效外部 target，则结果应是稳定空转：

```text
feedback 回到 L
output O 没有有效外部 target
=> bounded/no-output
=> 不生成 deferred output
```

如果 output 口确实命中了外部 target，却得到 `bounded/no-output`，那就是当前实现错误。正确结果应该是记录显式延迟输出，例如：

```text
bounded/deferred-output
ready tick = current tick + a
```

未来如果加入 strength，它是否成为“不断增大的 push 增幅器”必须由显式 policy 决定，不能从 action 名字、tag、entity 形状或闭环拓扑硬推：

```text
feedback strength 是否跨 tick 保留
同 tick 多输入如何 merge
output strength 是否叠加、衰减或封顶
dedupe key / causality id 如何防止同一 cause 重复增殖
```

没有这些 policy 时，闭环只能是周期输出，不是强度增幅器。

## 闭环是否移动

闭环 connected body 本体不一定移动。

如果闭环结构的 push 回到自身，并且同一 tick 内同一 subject 只能消费一次，那么它不能在同一原子事务里“把自己推开自己”。

这种情况下它更像持续输出装置：

```text
输入 push
闭环吸收 / 传递信号
每 a tick 输出一次 push
本体保持不动
外部被输出方向命中的物体被推动
```

如果存在明确外部空位和可提交移动，则该 subject 可以按普通移动规则移动一次。是否移动由本 tick 的 claim / plan / commit 判断，不由闭环递归强行决定。

## 收敛与循环

原子事务仍然需要区分收敛和循环。

多路径收敛：

```text
branch A -> X
branch B -> X
```

同一个 subject `X` 在同一事务中被重复命中，应合并或跳过重复输入，不应当作失败。

真正循环：

```text
A -> B -> C -> A
```

如果在同一事务里需要再次消费已经消费过的 subject，则不能继续展开。它应被截断到当前原子边界：

```text
重复命中同一 subject => 本 tick 不再消费
有输出需求 => 记录为下一 tick deferred output
无输出需求 => 稳定空转
```

不同 subject 但共享部分 entity：

```text
candidate X = A,B,C
candidate Y = B,C,D
```

这表示 action-unit 所有权不清，仍应按冲突或 cycle 处理，不能让同一个 entity 被两个不同 subject 同时提交。

## 与 Pending 的关系

push propagation 不应该进入 `PendingRuleStates`，也不应该为了未来可能的等待型 action 保留 push 链条。

当前结论是：push 这条 parent-child pending chain 应该从运行时推传播里移除。

如果未来真的出现 parent action 必须等待 child action 成功/失败的系统，需要单独设计新的有界事务模型，而不是复用当前 push pending chain。那个模型必须先定义：

```text
child 数量上限
等待终止条件
cycle / convergence / partial overlap 规则
超时 / 取消规则
owner result 聚合规则
```

否则无限闭环和涌现装置会再次把链条复杂度打爆。

push 的语义应该是：

```text
当前 action 原子结算
source 是否移动由当前 action 自己决定
被挡住时生成 future action input 或 explicit output event
下一 tick / tick + cost 后作为独立 action 重新进入管线
当前 action 不等待未来 action 的结果
```

因此 push 不需要：

```text
parent-child push unit
push contact batch
owner 等 child 成功后再返回
push chain cycle
push chain depth
bounded/no-output 作为 push 传播结果
```

push 需要的是一个 tick-based deferred output queue，而不是 pending chain。

## 当前代码问题

旧问题是：

```text
candidate subject 已经整体出现在 chain 中
CanAddChild() 只看到 entity overlap
于是报 push chain cycle
```

这个问题表面上可以通过 subject-level duplicate / convergence 修掉，但这只是止血。

当前止血后的失败例子是：

```text
2026-05-10 00:42:40.3989  [C2G_MoveRequestHandler] request entity:1 target:(0,0) clientTick:920 hasBefore:True before:(0,-1)
2026-05-10 00:42:41.0013  [C2G_MoveRequestHandler] response entity:1 success:True final:(0,-1) moveErrorCode:0 reason:bounded/no-output clientTick:920 hasAfter:True after:(0,-1)
```

这个结果说明：

```text
pending 链没有继续爆炸
但是 feedback 被截断后没有生成下一 tick action input
所以装置不会持续输出
```

这不是目标模型。它只是安全停机。

目标模型应该是：

```text
tick N
  player/action 输入触发闭环 subject
  当前 action 原子完成
  source 不移动或按当前 claim/commit 决定
  生成 deferred output:
    spec id
    source subject
    output entity / output subject
    direction
    ready tick = N + cost
    causality id
    dedupe key

tick N + cost
  deferred output 作为新的 action input 进入 ActionSpecs
  再次按当前世界状态原子结算
```

## 后续实现原则

后续实现应遵守以下原则：

```text
1. 每个 subject 每 tick 最多消费一次 push。
2. 同 tick push 可以叠加到同一个 subject。
3. 同一 subject 重复命中是 convergence，不是 cycle。
4. 不同 subject 共享 entity 是 unsafe overlap。
5. 闭环反馈不能在同一事务内继续递归。
6. downstream push 输出最早在 tick + cost 生效。
7. 持续装置由 tick 迭代涌现，不由 pending chain 求全局终点。
8. push propagation 不创建 pending child unit。
9. push action 不等待 downstream action 的结果。
10. deferred output 是 world tick 输入，不是 pending state。
```

## 底层重构方向

需要把 push 从旧 pending handoff 模型迁移成 deferred output 模型。

### 旧模型

```text
ActionSpecs.ResolvePushableBlock()
  contacts -> child unit requests
  PendingRuleStates.AddHandoffActionState()
  Pending child ready
  child 成功 / 失败
  owner 聚合 result
```

这个模型的问题是：

```text
它把 push 当成 parent-child transaction
它需要维护 chain / batch / owner result
它会遇到 cycle、depth、convergence、partial overlap 等链条复杂度
闭环装置会被迫在一次 pending 链里求解
```

### 目标模型

```text
ActionSpecs.ResolvePushableBlock()
  contacts -> downstream subjects
  当前 action 原子结束
  生成 DeferredAction / ExplicitOutputEvent

StateDrivenRules
  本 tick 只提交当前 action 的有限结果
  收集 deferred outputs

WorldTick
  把 ready deferred outputs 放回 WorldActionQueue
  下一 tick 独立仲裁 / plan / commit
```

`DeferredAction` 至少需要：

```text
spec id
entity id
subject entity ids
direction / vector
created tick
ready tick
cost ticks
causality id
dedupe key
```

第一阶段可以不做 push strength，但字段设计不能阻止后续加入 strength / weight。

### Pending 的保留边界

本阶段不为 push 保留 Pending 依赖。

如果删除 push pending path 后 `PendingRuleStates` 没有真实非 push 调用方，可以删除或隔离它。

如果未来确实需要等待型事务，应另起 proposal，而不是复用这条链。新 proposal 至少要证明：

```text
事务是有界的
child 集合是有限的
等待一定终止
闭环不会无限展开
owner result 聚合不会依赖无穷子结果
```

Push 的原子涌现模型不使用 Pending。

## 需要验证的行为

后续测试至少覆盖：

```text
同一 tick 多个 push 命中同一 subject => 合并或跳过重复，只消费一次。
多分支收敛到同一 subject => 不报 cycle，不创建重复 deferred output。
不同 subject 部分重叠 => 仍失败。
闭环结构回到自身 => 当前事务不无限展开，也不创建 pending child。
闭环有外部输出方向 => 当前 tick 生成 deferred output，下一 tick 按 cost 继续产生输出。
闭环没有外部输出方向 => 本体稳定不动，不生成 deferred output。
日志中不应再出现 push chain depth exceeded。
需要区分 bounded/no-output 与 deferred-output。
```

## 一句话总结

DG 的动作系统应该是：

```text
局部规则原子提交，长期行为由 tick 迭代涌现。
```

不要把涌现行为塞进一次 pending transaction 里求完。
