# 行为层总体架构

## 核心结论

DG 的小 GAS 是服务端权威的 action behavior pipeline。

它的职责是：

```text
把输入事实和自动行为转换为 ActionRequest
按 ActionSpec 和最终世界事实选择目标、主体、策略和冲突结果
生成有限 action unit
提交权威 GameWorld 变化
输出 ActionFact / PresentationFact / WorldDelta
```

它不负责：

```text
Unity 本地规则裁决
客户端动画反推结果
Fantasy Handler 里写业务规则
Luban 表直接修改世界
任意脚本 VM
```

## 架构主语

最终主语是：

```text
ActionBehavior
ActionBehaviorInstance
BehaviorStateMachine
ReservationScope
ActionFact
```

当前代码里 `TimedActionUnit` 是过渡实现。它已经能表达跨 tick 生命周期、scheduled output、completion output 和 incoming reject，但它不是最终行为层总抽象。

目标关系是：

```text
ActionBehavior
  -> ImmediateBehaviorResult
  -> ActionBehaviorInstance
       -> BehaviorStateMachine
       -> ReservationScope
       -> ActionFact / CommitProposal / DeferredAction / ActionResult
```

普通 move 可以继续走 immediate fast path。跨 tick、持锁、预约资源、等待条件或需要后续输出的行为才进入 active instance。

## 分层边界

### 输入层

输入层只表达意图。

```text
InputIntent
  SourceKind
  ActorEntityId
  InputKind
  Direction
  TargetHint
  ClientInputId
  ClientTick
  CreatedServerTick
```

输入层不判断碰撞、不判断 push、不判断技能结果、不写最终坐标。

客户端提交的是声明，服务端承认后才成为权威输入事实。

### 配置层

配置层描述有限策略。

核心输入是：

```text
ActionSpec
BlockedResultPolicy
ActionPresentation
EffectSpec
EntityArchetype
ComponentKind
```

`ActionSpecId` 只用于查配置。规则层不得用 action 名字、entity 名字或 tag 组合硬编码行为。

允许配置表达：

```text
primitive
target selector
subject policy
blocked policy
handoff policy
conflict policy
plan rule
commit rule
cost ticks
```

不允许配置表达：

```text
如何扫描世界
如何提交 GameWorld
如何广播 delta
如何执行任意代码
```

### 规则层

规则层是小 GAS 的核心。

当前目标阶段包含：

```text
ActionRequest
ActionContext
Targeting
Gating
Subject Selection
Strategy
Claim
Arbitration
Planning
Commit
Timed / Active Instance
Deferred Output
ActionFact
```

规则层只依赖 Shared GameCore，不依赖 Unity，不依赖 Fantasy 协议类型。

### 权威世界层

`GameWorld` 是规则裁决的真相源。

它负责实体、组件、tag、坐标、空间索引、snapshot/delta 和 runtime effect settlement 的统一读写边界。

世界修改必须通过 commit 边界进入，不从客户端镜像、debug UI 或 Fantasy Handler 直接改权威结果。

### 服务端外壳

Fantasy 服务端负责：

```text
接收协议
校验 session / player / actor
写入输入队列
驱动 server tick
调用 Shared GameCore
广播 WorldDelta
```

Handler 不写行为规则。

### 客户端镜像与表现层

Unity 客户端负责：

```text
采集输入
提交 intent / debug request
接收 WorldDelta
更新 ClientMapWorld 镜像
把 PresentationFact 翻译成 playback plan
播放 GameObject / group / feedback
```

客户端表现层可以插值、easing、闪烁、轨迹和音效，但不能决定 success、bounce、impact、commit 或 deferred push 释放时机。

## 小 GAS 的内部阶段

目标运行时主线：

```text
WorldAction / DeferredAction / InputIntent
  -> ActionRequest
  -> ActionSpec lookup
  -> target selector
  -> tag/component gate
  -> subject selector
  -> registered IActionStrategy
  -> ActionClaim
  -> claim arbitration
  -> MovePlan / CommitProposal / DeferredAction / ActionFact
  -> commit resolver
  -> GameWorld mutation
  -> WorldDelta / PresentationFact
```

其中 strategy 负责把明确策略输入转换为 action unit 候选，不负责提交世界。

commit handler 负责落地世界变化，不负责重新解释行为名字。

blocked outcome handler 负责遇阻分支，不塞进具体策略类里。

## Immediate 与 Active

### Immediate behavior

适合：

```text
普通 move
spawn
remove
立即 apply runtime effect
```

特点：

```text
当前 tick resolve
当前 tick commit
不进入 active store
可以产生默认 ActionFact / PresentationFact
```

### Active behavior

适合：

```text
rotate success
rotate bounce
charge
door opening
等待条件的机关
带预约资源的持续行为
```

特点：

```text
创建 ActionBehaviorInstance
持有 subject lock
持有 cell/resource reservation
按状态机释放 scheduled output
完成时释放 completion output
完成后释放 reservation
```

当前默认 incoming policy 是 `RejectIncoming`。等待、抢占、替换、排队都必须后续显式设计，不默认隐藏启用。

## 原子事务原则

一次 action / tick 的事务必须有限。

```text
输入有限
subject 有限
commit 有限
deferred output 有限
结果必须完成、失败、取消或生成未来 tick 输入
```

push chain、闭环装置和持续输出不能在同一事务内无限展开。它们通过多个 tick 的 deferred action 涌现。

正确语义是：

```text
当前 action 只处理当前事实
下游 push 进入未来 ready tick
当前 action 不等待下游结果
```

## 事实边界

行为层目标事实是 `ActionFact`。

同步和表现边界是 `PresentationFact`。

关系是：

```text
ActionFact
  -> projection
PresentationFact
  -> client translator
BehaviorPlaybackPlan
  -> runtime track
```

`PresentationFactComposer` 不应该成为特殊行为解释器。复杂行为必须由自身 behavior/state machine 输出显式事实。

## rotate 的位置

rotate 是 special behavior，不是核心架构。

它用于验证：

- connected body subject。
- active behavior lifecycle。
- subject lock。
- cell reservation。
- scheduled impact output。
- deferred push。
- group presentation fact。
- 客户端刚体 group playback。

rotate 的完整业务规则放在 `docs/Goal/Acion/special/rotate/rotate-architecture.md`，不写进核心行为层。

## 扩展原则

新增行为时按以下顺序判断：

1. 现有 `ActionSpec` 字段和策略能否表达。
2. 是否只需要新增配置数据和测试。
3. 是否需要新增 `IActionStrategy`。
4. 是否需要新增 blocked outcome / commit handler / targeting selector。
5. 是否需要 active behavior instance。
6. 是否需要新的 ActionFact 投影。

不允许为了一个新玩法直接在中心执行器里按名字加分支。
