# 表现层 Group 播放架构目标

## 背景

旋转连接体暴露了表现层的核心问题：有些动画的最小播放单位不是单个实体，而是一组实体。

普通单体移动可以用 `from -> to` 表达，表现层按每个实体播放也基本可接受。但连接体旋转、碰撞返回、端口线跟随、动画打断都要求一组实体在播放期间像一个刚体一样被管理。

因此表现层不能继续只围绕 `EntityAnimationEvent` 组织。表现层需要正式支持 group 作为一等播放对象。

## 核心决策

### 已定决策

当前已经确认：

```text
表现层需要 group 作为一等播放对象
group 的权威成员由逻辑层产生
表现层不自己重建 connected group
服务端同步表现语义，不同步 Unity 动画帧
客户端根据 PresentationFact 组装 BehaviorPlaybackPlan
当前按原子行为处理，不引入跨 action 动作链条
RigidBodyGroupTrack 同时承接 connected body push 和 rotate
```

这些是后续 OpenSpec 和实现时的边界，不应该在实现时再退回到 per-entity 特例补丁。

### Group 是表现层一等播放对象

表现层必须支持：

```text
BehaviorPlaybackPlan
  RigidBodyGroupTrack
  EntityTrack
  PortConnectionTrack
  FeedbackTrack
```

其中 `RigidBodyGroupTrack` 负责一组实体的整体播放。单体动画只处理不属于 group 的实体。

### Group 成员由逻辑层产生

表现层不重新扫描端口图，也不自己推导连接体成员。

逻辑层负责产生权威 group 信息：

```text
BodyId
BodyKind
Members
From
To
Pivot
RotateDirection
ResultKind
StartTick
ContactTick
EndTick
ContactProgress
```

表现层只消费 `PresentationFact` 中携带的成员和行为语义。

原因：

```text
服务端才知道权威规则结果
客户端 AOI 可能不完整
客户端本地端口图可能不是事实发生时的图
表现层不应该重新实现复杂规则
```

### 服务端发语义事实，不发 Unity 动画细节

服务端不应该发送每帧角度、缓动比例、Transform 位置。

服务端应该发送：

```text
这次行为是什么
影响哪些实体
结果是什么
发生在哪个 tick 窗口
最终权威状态是什么
```

客户端根据这些事实生成播放计划。

这里同步的是表现语义，不是精确动画。

允许：

```text
客户端本地决定缓动曲线
客户端本地决定 phase 内部进度
不同客户端动画相位略有差异
```

不允许：

```text
客户端猜 group 成员
客户端猜复杂规则结果
客户端只靠 before/after snapshot 推断碰撞返回
客户端把表现播放写回权威镜像
```

### BehaviorPlaybackPlan 是表现层的行为动画包

客户端收到 `PresentationFact` 后，先归并成行为动画包，再生成轨道。

当前阶段主键：

```text
SourceActionId + StartTick + EndTick
```

`ContactTick` 不作为归并主键。它是同一个行为包内部 phase 的边界。

兼容路径：

```text
ServerTick + FactId
```

当前系统仍按原子行为理解，不引入跨 action 的动作链条合并。未来如果一个视觉整体确实跨多个 `SourceActionId`，再扩展为 `PlaybackGroupId`。

当前不引入：

```text
OwnerActionId 归并
CausalityId 归并
跨 SourceActionId 的 PlaybackGroupId
deferred action 链条表现合并
```

保留扩展点即可，不把未来问题提前塞进当前实现。

## 约束

### 对齐逻辑层当前实现

逻辑层当前已经有 `TimedActionUnit`、`TimedActionScheduler`、`TimedActionScheduledOutput` 和 `TimedActionOutput`。

表现层文档必须按这个事实理解：

```text
RotatePivotGroup start fact 在 StartTick 产生
RotatePivotImpact scheduled fact 在 ContactTick 释放
completion output 在 EndTick 释放
ContactTick / ContactProgress / EffectiveCostTicks 已经通过 PresentationFact 下发
```

当前 tick 内顺序为：

```text
ReleaseReadyTimedActions
-> 处理本 tick incoming actions
-> rotate/push/arbitration/commit
```

因此表现层不能假设 impact 与 rotate start 在同一 tick，也不能自己推导 impact 释放时机。

`DeferIncomingUntilEnd` 当前只是逻辑层枚举占位。表现层不应该把它当成已支持语义；当前表现层只需要处理 `RejectIncoming` 下的权威结果和后续权威事实接管。

### 不写权威状态

group 播放只影响表现层 runtime。

不允许写入：

```text
ClientMapWorld
Client authoritative mirror
逻辑层占用数据
端口连接规则数据
```

播放完成后只对齐 after snapshot。

### 不做客户端规则推导

表现层不应该调用规则层去重新计算：

```text
connected group 成员
旋转是否成功
碰撞对象是谁
最终坐标
端口连接关系的权威结果
```

这些必须来自服务端事实和 after snapshot。

### 不把 rotate 做成孤立特例

`RotatePivotGroup` 是第一个落地点，但底层抽象必须是 group 播放。

目标不是：

```text
RotatePivotGroupRuntime 特例继续变大
```

目标是：

```text
RigidBodyGroupTrack.RotateAroundPivot
RigidBodyGroupTrack.Translate
```

### 不在当前阶段解决动作链条

当前前提：

```text
一次表现行为 = 一个 SourceActionId
一个 SourceActionId 内可以有多个 PresentationFact
```

如果未来一个视觉整体跨多个 action，再通过新的 `PlaybackGroupId` 或等价字段扩展。

### 不要求动画精确网络同步

网络包不传播放帧。

表现层只需要足够的语义边界：

```text
SourceActionId
StartTick
ContactTick
EndTick
ResultKind
Members
Pivot
Motion
Final authoritative snapshot
```

客户端用这些信息本地播放，并在结束时收敛到权威状态。

对于 rotate bounce，表现层必须优先使用 `ContactTick / ContactProgress` 划分 phase。只用 `EndTick - StartTick` 播放完整 bounce 只能作为旧协议兼容路径，不能作为目标实现。

## Tradeoff

### 只修旋转特例

优点：

```text
改动短
短期能缓解当前错乱
```

缺点：

```text
connected body push 仍然是多个单体动画
端口线 ownership 仍然散
打断仍然没有统一边界
后续 bounce、push、rotate 会继续各自补丁
```

### 正式引入 group 播放底层

优点：

```text
push 和 rotate 统一
连接体视觉上真正作为刚体播放
ownership 边界清楚
打断、端口线、最终对齐有统一入口
```

缺点：

```text
需要新增 BehaviorPlaybackPlan / scheduler / track ownership
需要把现有 rotate runtime 迁入新结构
普通 connected body push 后续也要升级 fact
```

当前选择：

```text
正式引入 group 播放底层
先从 RotatePivotGroup 接入
再升级 connected body push
```

## RigidBodyGroupTrack 职责

`RigidBodyGroupTrack` 负责：

```text
拥有 group 成员 view
统一写入成员位置、旋转、scale
统一计算当前视觉 pose
统一管理端口线端点
处理开始、播放、返回、完成、打断
完成后释放所有权并对齐 after snapshot
```

播放期间，普通 entity animation 不能写 group 成员。

表现层需要维护所有权：

```text
entityId -> trackId
connectionKey -> trackId
```

`ClientWorldVisuals` 刷新实体和连接线前必须先检查 ownership。

## Motion 类型

当前目标只需要两个 group motion：

```text
Translate
RotateAroundPivot
```

连接体 push 使用 `Translate`。

连接体旋转使用 `RotateAroundPivot`。

不要先抽象过多 motion 类型，后续有真实需求再扩展。

## 旋转与碰撞返回

旋转成功：

```text
RotateOut
CommitFinalPose
```

旋转碰撞返回：

```text
RotateOut
ImpactFeedback
RotateBack
RestoreAuthoritativePose
```

碰撞返回不应该只用 `sin(progress) * angle` 糊成一个 bounce。底层阶段必须明确，否则 cost tick、打断、连接线和最终对齐都会继续混乱。

阶段边界必须来自逻辑层事实：

```text
RotateOut: StartTick -> ContactTick
ImpactFeedback: ContactTick
RotateBack: ContactTick -> EndTick
RestoreAuthoritativePose: EndTick
```

表现层可以决定每个 phase 内部的缓动曲线，但不能自行猜测 `ContactTick`、`ContactProgress` 或 impact/deferred push 的释放时机。

如果逻辑层策略决定碰撞返回占用更多 `costTicks`，表现层只消费 `StartTick / EndTick` 映射出的时长。表现层不决定规则 cost。

最终位置始终对齐 after snapshot。`PresentationFact` 中的 attempted target 只能作为视觉中间目标，不能作为最终权威落点。

## Connected Body Push

当前逻辑层已有：

```text
BehaviorBody
MovePlan.BodyId
MovePlan.BodyKind
MovePlan.Members
```

但普通 connected body push 到表现层时多数仍被拆成多个 `EntityPushed`，客户端再生成多个单体动画。

这对纯平移暂时可用，因为所有成员方向和时长通常一致，看起来像整体移动。但这不是最终表现层底层。

目标架构中，connected body push 也应该走 group fact：

```text
PresentationFactType.BodyMoved
  BodyId
  BodyKind
  Members
  Direction
  StartTick
  EndTick
```

客户端生成：

```text
RigidBodyGroupTrack.Translate
```

## Rotate Pivot Group

`RotatePivotGroup` 已经携带 members，是当前最适合先接入 group 播放底层的事实类型。

目标是把现有临时 rotate runtime 收拢到正式轨道：

```text
PresentationFact.RotatePivotGroup
  -> BehaviorPlaybackPlan
  -> RigidBodyGroupTrack.RotateAroundPivot
```

`ClientWorldVisuals` 不应该继续直接管理 rotate 特例。它应该只渲染 playback scheduler 提供的当前视觉状态。

## 权威接管与重同步

逻辑层目标中，rotate 是 guaranteed physical timed action，普通外部行为默认不能中途打断它。

所以表现层这里的接管不是规则打断，不代表客户端可以自行取消 rotate。它只处理以下情况：

```text
收到更新的权威 PresentationFact
收到更高 tick 的 snapshot 需要重同步
未来显式 cancel 行为产生了权威事实
同一成员出现新的权威 group 播放事实
```

当新权威 group 播放事实与旧 track 成员重叠：

```text
旧 track 进入 Interrupted
旧 track 停止写 Transform
旧 track 释放 ownership
新 track 接管成员
```

第一步可以从 after/before pose 启动新 track。后续如果视觉需要更顺滑，再支持从当前 visual pose blend。

接管的关键不是一开始就做复杂过渡，而是保证旧 track 不会继续写成员和连接线。

## 数据流

目标数据流：

```text
WorldDelta
  changedEntities
  presentationFacts
        |
        v
ClientPresentationPlaybackPlanner
        |
        v
BehaviorPlaybackPlan[]
        |
        v
PresentationPlaybackScheduler
        |
        v
Runtime Tracks
        |
        v
ClientWorldVisuals
```

`ClientWorldVisuals` 不再决定某个行为怎么播，只负责把当前 runtime pose 画出来。

## 当前实现缺口

当前播放层结构已经基本完整，主要风险不在 group 播放架构，而在权威数据契约和事实生成一致性。

### P0. BodyId / BodyKind 必须下发到客户端

`BodyId / BodyKind` 是 group 播放事实的权威身份字段。

服务端 `ActionFact` 已经有 body 身份，但客户端 `ClientPresentationFact` 必须也携带：

```text
BodyId
BodyKind
```

否则客户端只能通过 `Members`、tick 和 fact id 间接判断 group 身份。普通场景可能可用，但当多个 body 实例共享成员、成员重叠或同 tick 产生多个 group fact 时，ownership 仲裁可能把不同 body 的播放轨道混在一起。

修正范围：

```text
protobuf PresentationFact 字段
服务端 PresentationFactComposer 填充
客户端 ClientPresentationFact 接收
BehaviorPlaybackPlan / RigidBodyGroupTrack 保存 body 身份
```

验证点：

```text
BodyMoved / RotatePivotGroup 到客户端后保留 BodyId / BodyKind
两个成员重叠但 BodyId 不同的 group fact 不会归并成同一个 track
ownership 日志或测试断言能区分 entityId 属于哪个 body track
```

### P1. BodyMoved 与成员 EntityPushed 不能同时输出

connected body push 一旦产生 `BodyMoved` group fact，同一 tick、同一 source action、同一 body 的成员就不应该再输出单体 `EntityPushed`。

客户端可以保留防御性过滤，但不能把客户端过滤当成主要一致性策略。因为如果消息顺序变化，单体 `EntityPushed` 先被消费，客户端仍可能先生成 per-entity track，再被 group track 接管，造成双播或 ownership 抖动。

目标约束：

```text
PresentationFactComposer 在 source 端抑制冲突 fact
BodyMoved 覆盖的成员不再产生同源 EntityPushed
客户端过滤只作为兼容旧事实的兜底
```

验证点：

```text
同一 tick 的 connected body push 只生成一个 BodyMoved
BodyMoved.Members 中的实体没有同源 EntityPushed
客户端接收顺序变化时不会出现双播
```

### P2. Bounce phase 需要可观测

`RigidBodyGroupTrack.RotateAroundPivot` 可以继续使用本地缓动曲线，但运行时应该能暴露当前 phase：

```text
RotateOut
ImpactFeedback
RotateBack
RestoreAuthoritativePose
```

这不是立即影响正确性的前置项，但会影响后续打断、重同步和调试。当前连续 progress 函数如果不暴露 phase，日志和测试只能看到位置变化，看不到为什么在某个 tick 切换了行为。

验证点：

```text
ContactTick 前 phase 为 RotateOut
ContactTick 当 tick 可观测到 ImpactFeedback
ContactTick 到 EndTick 为 RotateBack
EndTick 后进入 RestoreAuthoritativePose 并对齐 after snapshot
```

## 分步落地

### 0. 补齐 group fact 身份字段

先补 `BodyId / BodyKind` 下发。

这是后续 group ownership 演化的前置条件。没有 body 身份，客户端只能按成员集合和 tick 做弱归属，无法稳定处理多 body、重叠成员和重同步接管。

验证点：

```text
服务端 composer 能从 ActionFact / MovePlan 写入 BodyId / BodyKind
客户端收到 PresentationFact 后字段值不丢失
BehaviorPlaybackPlan 的归并不会吞掉不同 BodyId 的 group fact
```

### 1. RotatePivotGroup 接入 RigidBodyGroupTrack

把现有 rotate group runtime 收拢为正式 group track。

该步骤必须消费逻辑层已经下发的时间语义：

```text
StartTick
ContactTick
EndTick
ContactProgress
EffectiveCostTicks
```

验证点：

```text
progress 0.5 时成员世界坐标等于绕 pivot 旋转的结果
bounce 完成后成员回到 after snapshot
ContactTick 对应 impact phase 边界
普通 activeAnimations 不覆盖 group 成员
```

### 2. 连接线 ownership

active group 内的连接线由 group track 管理。

group 成员与外部实体连接线，播放期间要么隐藏，要么由 group 当前 pose + 外部 snapshot 计算。

验证点：

```text
播放中连接线端点跟随成员当前视觉位置
不会被最终 snapshot 位置覆盖
```

### 3. Connected body push 输出 BodyMoved fact

逻辑层从 `MovePlan.Members` 产生 group move fact。

客户端生成 `RigidBodyGroupTrack.Translate`。

验证点：

```text
连接体 push 只有一个 group track
成员相对偏移播放期间保持不变
单体 EntityPushed 不重复播放 group 成员
```

### 4. Group 权威接管

新权威 group 播放事实接管重叠成员，旧 track 停止写入并释放 ownership。

验证点：

```text
重叠成员不会被旧 track 回写
新 track 可以稳定接管
最终仍对齐 after snapshot
```

## 测试要求

自动化测试使用 Unity 自带 TestFramework。

至少需要：

```text
RotatePivotGroup 中途坐标测试
RotatePivotGroup bounce 最终权威落点测试
连接线端点跟随 group 当前 pose 测试
connected body push 刚体平移测试
group 权威接管 ownership 测试
BodyId / BodyKind 下发与归并隔离测试
BodyMoved 抑制同源成员 EntityPushed 测试
Rotate bounce phase 边界测试
```

手动端到端验证：

```text
启动服务端
启动 Unity Play Mode 客户端
触发连接体旋转成功
触发连接体旋转碰撞返回
触发连接体 push
观察成员不撕裂
观察端口线跟随
观察最终状态与服务端一致
```

## 当前结论

表现层确实需要 group。

这个 group 不是规则层新实体，也不写入 `ClientMapWorld`。

它是表现层播放轨道，用于在播放期间统一管理一组实体的视觉状态。

逻辑层负责输出权威成员和行为语义，表现层负责播放。
