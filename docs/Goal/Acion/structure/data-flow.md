# 行为层数据链路

## 总览

DG 行为层的数据链路分成两条线：

```text
配置线：Luban / fallback provider -> ActionSpec / EffectSpec / EntityArchetype -> GameCore registry
运行线：InputIntent / WorldAction / DeferredAction -> ActionRequest -> behavior pipeline -> GameWorld -> WorldDelta
```

最终所有权威结果都以服务端 `GameWorld` 为准。客户端只消费 `WorldDelta`、snapshot 和 `PresentationFact`。

## 配置数据链路

```text
Excel / JSON 源数据
  -> Luban 生成 C# 表代码
  -> Luban 生成 JSON 数据
  -> Unity StreamingAssets / Server config path
  -> LubanGameConfigProvider
  -> IGameConfigProvider
  -> ActionSpecRegistry / EffectSpecRegistry / EntityBuilder
  -> GameWorld / ActionRuntime
```

重要边界：

- Luban 生成类型不能直接污染规则执行层。
- provider 负责把表结构转换成 GameCore 稳定模型。
- fallback provider 只能显式用于测试或兼容，不应在正式服务端静默接管。
- 跨表引用必须可测试。

### ActionSpec

`ActionSpec` 是行为策略入口。

它至少决定：

```text
spec id
primitive / strategy
source
priority
targeting selector
source / ability / required / blocked tag policy
blocked result policy
handoff policy
subject policy
plan rule
commit rule
default cost ticks
```

规则层只读这些字段和最终世界事实，不根据 `mechanism_push`、`player_push` 等名字推断特殊逻辑。

### EntityArchetype 与 Component

实体出生链路：

```text
EntityArchetype
  -> ComponentKind list
  -> ComponentApplicationRegistry
  -> GameWorld.SetComponent
```

component 表示实体能力和规则事实输入。它不是完整技能系统。

可查询 component 需要进入 `ComponentFactQueryRegistry`，让 targeting、condition、blocked policy 用 component id 查询最终事实。

### RuntimeEffect

runtime effect 是运行时状态来源。

目标链路：

```text
EffectSpec
  -> EffectApplication
  -> RuntimeEffectStore
  -> ComponentSourceContribution / TagSourceContribution
  -> ComponentStateResolver / tag settlement
  -> final component/tag fact
  -> action pipeline query
```

静态 component 和 runtime component source 必须共存，移除 buff 时只移除对应来源，不误删静态来源。

## 输入数据链路

客户端和调试工具不应该直接生成最终 action result。

目标链路：

```text
Unity Input System / Debug UI / AI / Replay / Server mechanism
  -> InputIntent 或 WorldAction
  -> server validation / authorization
  -> InputIntentBuffer / WorldActionQueue
  -> IntentAdapter / ActionRequestAdapter
  -> ActionRequest
```

### InputIntent

`InputIntent` 表示输入事实，不表示行为结果。

包含：

```text
来源
actor
输入类型
方向或目标提示
客户端输入 id
客户端 tick / sample time
服务端创建 tick
```

不包含：

```text
最终坐标
碰撞结果
push 结果
commit proposal
WorldDelta
```

### DeferredAction

`DeferredAction` 是未来 tick 的正式行为输入，不是动画 metadata。

典型来源：

```text
blocked DeriveAction
push handoff
rotate impact push
mechanism delayed output
```

进入队列后，它必须重新走正式 action pipeline：

```text
DeferredAction
  -> WorldActionQueue
  -> ready tick drain
  -> ActionRequest
  -> strategy / claim / planning / commit
```

上游 action 不等待下游 deferred action 成功或失败。

## 行为运行数据链路

一次 ready action 的主链路：

```text
ActionRequest
  -> ActionSpecRegistry.Get(specId)
  -> TargetingSystem
  -> ActionTagGate / component conditions
  -> ActionSubjectSelector
  -> ActionStrategyRegistry
  -> IActionStrategy.Build
  -> ActionClaim
  -> ActionArbiter
  -> RulePlanning
  -> CommitProposal
  -> CommitResolver
  -> GameWorld mutation
```

### TargetData

targeting 只根据 request、spec 和权威世界事实解析目标。

客户端 `TargetHint` 只是提示。服务端必须重新解析。

### Subject

subject 是 action 的所有权单位。

可能是：

```text
单实体
connected body
front entity set
none / self
```

同一 tick 同一 subject 应该只消费一次合并后的输入，不能在同一事务里反复消费。

### Claim

claim 描述 action unit 对实体、格子或资源的有限声明。

claim arbitration 只比较候选声明，不在中心层构造行为特例。

### Plan 与 Commit

planning 生成计划，commit 负责落地世界修改。

commit handler 可以修改：

```text
Position
Direction
component/tag source
entity create/remove
runtime effect store
```

commit handler 不应该重新解析 action 名字，也不应该处理客户端表现。

## Active 行为数据链路

跨 tick 行为的目标链路：

```text
ActionRequest
  -> behavior resolver
  -> ActionBehaviorInstance
       subject lock
       cell/resource reservation
       state data
       scheduled output
       completion output
  -> active store
  -> later tick release scheduled output
  -> end tick release completion output
```

当前代码过渡形态是：

```text
TimedActionUnit
  StartFacts
  ScheduledOutputs
  CompletionOutput
  ReservationScope
```

目标上它应归入 `ActionBehaviorInstance`。

## 事实同步数据链路

服务端行为事实目标：

```text
ActionFact
  -> ActionFactProjection
  -> PresentationFact
  -> fact id assignment
  -> WorldDelta
```

客户端表现链路：

```text
WorldDelta
  -> ClientMapWorld.ApplySnapshot / ApplyDelta
  -> ClientPresentationFactTranslator
  -> ClientPresentationPlaybackPlanner
  -> BehaviorPlaybackPlan
  -> PresentationPlaybackScheduler
  -> RigidBodyGroupTrack / EntityTrack / FeedbackTrack
  -> GameObject visual pose
```

客户端镜像状态和视觉播放状态必须分离：

```text
ClientMapWorld = 权威镜像
PlaybackTrack = 临时视觉 pose
```

视觉播放不能写回权威镜像来影响规则。

## rotate 数据链路示例

```text
push ActionRequest
  -> connected body subject
  -> pivot count
  -> torque contribution
  -> rotate direction
  -> rotate sweep
```

无碰撞：

```text
start tick
  -> RotatePivotGroup Success fact
  -> active timed unit
end tick
  -> commit body final coords/directions
  -> completion result
```

有碰撞：

```text
start tick
  -> RotatePivotGroup Bounce fact
  -> active timed unit
contact tick
  -> RotatePivotImpact fact
  -> DeferredAction push
end tick
  -> no coord commit
  -> completion result
```

待升级点：

```text
CostTicks 应直接切分 90 度弧段
每个 tick 弧段做 connected body shape sweep
同一 contact tick 弧段内所有 blocker 一起收集
```

## 数据所有权

| 数据 | 所有者 | 可被谁写 | 可被谁读 |
|---|---|---|---|
| `ActionSpec` | 配置层 | provider / registry 构建 | 行为层 |
| `InputIntent` | 输入层 | 客户端声明 / 服务端承认 | adapter |
| `ActionRequest` | 行为层入口 | queue / adapter | targeting / strategy |
| `GameWorld` | 服务端权威 | commit / world bootstrap / debug authority | 规则层 / sync |
| `ActionFact` | 行为层 | behavior / state machine | projection / server diagnostics |
| `PresentationFact` | 同步层 | projection / composer fallback | Unity presentation |
| `ClientMapWorld` | 客户端镜像 | WorldDelta applier | presentation / debug view |
| `PlaybackTrack` | 客户端表现层 | scheduler / track runtime | visuals |
