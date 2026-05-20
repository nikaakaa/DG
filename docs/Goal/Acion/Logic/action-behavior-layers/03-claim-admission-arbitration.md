# Claim / Admission / Arbitration

## 结论

Claim 是 commit 前的竞争意图。它不是世界写指令,也不是行为事件。

完整链路:

```text
Action input
  -> Claim              我想占什么
  -> Arbitration        谁赢,谁被拒
  -> CommitProposal     获胜者真正要改什么
  -> CommitResolver     写 world
```

## 为什么 commit 时检查不够

场景:

```text
A 在 (3,5), 高优先级, 想去 (5,5)
B 在 (7,5), 低优先级, 想去 (5,5)
同一个 tick
期望: A 赢,B 拒绝
```

如果没有 Claim,只靠 commit 时检查,常见方案都会坏:

```text
按队列顺序 commit:
  队列顺序影响结果。B 如果先执行就可能先占成功。

按优先级排序 commit:
  单 tick 简单 move 能工作,但多 tick 行为已经走到一半时无法表达长期占用。

先全部 commit 再 rollback:
  world 会短暂进入非法状态,回滚成本高,网络重放和审计复杂。

其他行为主动检查谁在跑:
  Runner 之间互相认识,DoD 失效。
```

Claim 的价值:

```text
order-independent
deterministic
replay-friendly
conflict reason 清晰
支持跨 tick 占用
```

## Claim 和 CommitProposal 的区别

```text
Claim:
  我想占用 subject / cell / resource
  admission 阶段使用
  可以跨多 tick 持有
  失败时不写 world

CommitProposal:
  我已经获胜,现在要改 world
  apply 阶段使用
  原子、一次性
  不表示长期占用
```

不要用 empty CommitProposal 当占位。那会污染 CommitProposal 的语义。

## 长期占用

rotate 5 tick:

```text
tick 1 Anticipating  无 world 变更
tick 2 Contacting    无 world 变更
tick 3 Contacting    无 world 变更
tick 4 Recovering    无 world 变更
tick 5 Completing    emit MoveEntity proposal
```

如果 rotate 期间要占住 pivot entity 和相关 cells,应该:

```text
tick 1 acquire SubjectClaim + CellClaim
tick 1-5 claim 持续有效
Exit 或 interrupt 时释放
```

不要:

```text
每 tick emit empty commit
CommitProposal 加 lifespan
让别的 Runner 主动检查 rotate 是否在跑
```

## 最小 Claim 集合

当前最小集合:

```text
SubjectClaim      占用实体/行为主体
CellClaim         占用目标格
ClaimMode         Exclusive / Shared
```

可延后:

```text
ResourceClaim     等 mana / inventory / cooldown 等资源竞争出现再加
ClaimChannel      等 Subject/Cell/Resource + mode 不够表达真实冲突域再加
ClaimPriority     优先级应来自 ActionSpec / request,不要先做成 claim 子系统
```

## 哪些行为需要 Claim

需要:

```text
step_runner:
  想移动实体,需要 SubjectClaim + CellClaim

rotate_runner:
  多 tick 占用 pivot/body/cells,需要长期 Claim

open_door:
  如果门状态只能被一个 action 改,需要 SubjectClaim(door entity)
```

不需要:

```text
shout_runner:
  原地 emit BehaviorEvent,不改 world,不抢资源

cast_freeze:
  如果只允许多个 freeze 叠加,可不需要 Claim
  它输出 AddRuntimeEffect 即可
```

Claim 是仲裁工具,不是每个行为的必备输出。

## Claim 释放

Claim 在 Runner 生命周期结束时释放:

```text
自然完成:
  Completing emit commit -> Exit -> release claims

被打断:
  Interrupt -> Exit -> release claims

失败/取消:
  terminal state -> release claims
```

不要每 tick 自动释放再重新 acquire。那会让跨 tick 行为中间露出竞争空窗。
