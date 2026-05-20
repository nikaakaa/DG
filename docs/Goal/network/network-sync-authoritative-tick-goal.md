# DG 网络同步权威 tick 架构目标

## 定位

这份文档记录 DG 后续网络同步、输入预测、节奏判定、AOI/chunk 和大规模实体同步的目标架构。它不是 OpenSpec 提案，也不是实现任务清单。后续要落地其中某一块能力时，必须再通过 OpenSpec 拆成可测试的变更。

核心目标是：

```text
逻辑绝对不能错。
客户端必须有即时响应。
服务端永远是权威真相源。
客户端预测只能存在于本地预测 overlay。
复杂世界规则不做客户端全量回滚。
大量实体同步必须依赖 AOI/chunk、dirty delta 和 active set。
```

最终架构名称：

```text
服务端权威 tick 世界
+ 本地玩家预测 overlay
+ 服务端输入延迟补偿
+ 服务端校正和未确认输入重放
+ 远端实体插值
+ AOI/chunk dirty delta 同步
+ PresentationFact 表达复杂事件事实
```

这才是 DG 语境下的 Minecraft-like。它不是让客户端完整模拟世界，而是让客户端先响应本地玩家输入，同时让服务端保留最终裁决权。

## 不做什么

不做全世界 rollback。

不让客户端预测旋转机关、连接体、多实体连锁、伤害、掉落、奖励和最终 perfect/failed 判定。

不让客户端把预测结果写入普通 `ClientMapWorld` 或任何看起来像权威镜像的状态容器。

不把 `PresentationFact` 当成规则结果。它只解释权威状态变化背后的表现事实。

不靠可靠有序传输保证所有状态包必达。实时状态必须能从较新的权威快照恢复。

## 核心原则

### 服务端权威

真实世界状态只在服务端规则层改变。

```text
Authoritative GameWorld
  -> Authoritative Tick
  -> WorldDelta
  -> Client Authoritative Mirror
```

客户端可以显示、预测、插值、校正，但不能提交真实规则结果。

客户端不能决定：

```text
最终坐标
最终占用
碰撞结果
推动结果
旋转机关结果
connected body 结果
perfect / failed 最终判定
伤害、死亡、掉落、奖励
```

### 客户端预测是 overlay

客户端普通权威镜像只保存服务端确认过的状态。

本地玩家预测必须放在独立的 `PredictedLocalOverlay` 或等价结构里：

```text
ClientAuthoritativeState
  服务端最近确认的镜像

PredictedLocalOverlay
  本地玩家基于未确认输入得到的临时预测状态

PresentationState
  动画、插值、特效和显示状态
```

本地预测可以影响：

```text
本地玩家手感
本地玩家视觉位置
本地玩家继续预测时使用的临时状态
```

本地预测不能影响：

```text
普通 ClientWorld 实体状态
其他实体规则判断
机关触发
伤害、占用、掉落、奖励
WorldDelta 内容
```

所以这仍然是预测回滚，只是回滚对象不是世界，而是本地玩家预测 overlay：

```text
收到服务端权威玩家状态
  -> overlay 基准回滚到权威状态
  -> 删除已确认 input
  -> 重放未确认 input
  -> 得到新的预测 overlay
  -> 表现层平滑或显式校正
```

## 本地玩家预测边界

客户端只预测本地玩家基础移动。

允许预测：

```text
朝向
普通离散移动
基础占用/阻挡判断
输入缓冲后的本地 action intent
```

不预测：

```text
旋转机关
connected body 连锁
多实体挤压
复杂 push handoff
伤害和死亡
奖励和掉落
AOI 外实体影响
```

Shared 代码的价值在于让客户端和服务端共享基础移动预测所需的纯数据规则，但共享范围必须克制。Shared 预测函数应该接近：

```text
PredictedPlayerState ApplyInput(
  PredictedPlayerState state,
  PlayerInputIntent input,
  PredictionWorldView view)
```

`PredictionWorldView` 是预测用轻量世界视图，不是完整权威世界：

```text
StaticBlockMap
KnownDynamicOccupancy
LocalPlayerState
Tick / ActionWindow
```

预测规则必须纯数据、离散、可重放，不依赖 Unity `Transform`、`Time.deltaTime`、场景对象或不稳定集合遍历顺序。

## 输入响应和权威结果

DG 的客户端必须即时响应输入，但响应不等于承诺成功。

```text
Input Response
  我收到你的输入了

Authoritative Result
  服务端确认这个输入造成了什么结果
```

输入发生时，客户端可以立刻给出：

```text
朝向变化
起步反馈
按键音效
节奏 UI 反馈
短距离预测移动
```

服务端返回后，客户端再确认：

```text
Success
Perfect
Good
Failed
Blocked
Corrected
FactOwned
```

普通移动可以采用 C-lite：

```text
本地玩家基础移动完整预测
服务端不同意时校正
复杂事件由服务端事实接管
```

这接近 Minecraft 的手感策略，但不会把复杂世界规则推给客户端。

## 节奏输入和延迟补偿

规则层按离散 tick/action window 结算。长按、连发、预输入属于输入层问题，由输入层转换成离散 intent。

输入采集和发送必须即时：

```text
玩家按下
  -> 客户端立刻响应
  -> 客户端立刻发送 input intent
  -> 服务端在对应 action window 消费
```

服务端不能只按收包时间判定 perfect/failed。输入包必须携带本地采样时间或节奏偏移：

```text
inputId
clientPressTick / clientPressTime
estimatedServerTick
beatOffset
actionKind
direction
```

服务端维护客户端到服务端时间轴的偏移估计，把输入映射到权威节奏轴上，再判定：

```text
Perfect
Good
Early
Late
Failed
Blocked
```

延迟补偿只补偿输入时间，不回看历史世界状态。

也就是说：

```text
补偿玩家按下时落在哪个节奏窗口
不把服务端世界 rollback 到过去重新算
```

必须限制：

```text
最大补偿时间
最大未来输入容忍
输入序号单调
同一 action window 只消费一个主动作
客户端时钟偏移不能突然跳变
过旧输入直接拒绝或降级
```

这样可以避免高延迟玩家被网络冤枉，同时不允许客户端随便伪造 perfect。

## WorldDelta 语义

服务端输出以 `WorldDelta` 为主，input ack/result 是 `WorldDelta` 的一部分，而不是单独用 input result 驱动世界。

推荐结构语义：

```text
WorldDelta
  serverTick
  sequence
  changedEntities
  removedEntities
  presentationFacts
  lastProcessedInputId
  recentInputResults
  localPlayerAuthoritativeState
```

原因：

```text
玩家输入只是世界变化来源之一
怪物、机关、环境、多人冲突也会改变世界
PresentationFact 是世界级事实，不一定只属于某个输入
AOI/chunk 需要按 observer 裁剪 WorldDelta
```

`EntityDelta` 应优先发送当前权威字段值，而不是不可恢复的操作增量。

推荐：

```text
entityId = 5
coord = B
hp = 80
state = Moving
```

避免：

```text
entityId = 5
move +1
hp -10
```

这样客户端丢掉旧包后，收到新包仍然能回到服务端认可的状态轨道。

## 网络恢复语义

状态同步采用 tick/sequence 快照流思路。旧包可以丢，客户端只应用可接续的新状态。

状态类数据：

```text
带 serverTick / sequence
旧包丢弃
缺口过大时请求 full snapshot / resync
```

input ack：

```text
后续 delta 冗余携带 lastProcessedInputId
必要时携带 recentInputResults
客户端不能因为某个 ack 包丢失而永久保留 pending input
```

PresentationFact：

```text
带 factId / sourceActionId / startTick / endTick
active window 内可重复发送
客户端按 factId 去重
晚到时按 serverTick 推算当前进度
```

逻辑一致性来自：

```text
服务端权威状态
可恢复的当前字段值
input ack 冗余
factId 去重
客户端预测 overlay 可丢弃可重建
```

不是来自：

```text
所有包可靠有序
客户端完整模拟世界
```

## AOI / chunk 同步

大量实体不能全量同步给每个客户端。

AOI/chunk 采用混合模型：

```text
空间 chunk 是主索引
实体关系和事件是附加订阅
```

主同步：

```text
玩家所在 chunk
周围 chunk radius
可见或相关实体
```

附加同步：

```text
正在交互的 connected group
rotate group members
玩家绑定、锁定、战斗相关实体
active PresentationFact 覆盖的成员
```

如果只按 chunk，跨 chunk 的复杂 group 会断。  
如果只按关系，大量普通实体无法高效裁剪。

所以 DG 应该让 chunk 负责空间规模，关系订阅负责规则完整性。

## 服务端性能路径

1w+ entity 不能每 tick 全量跑规则。

服务端需要分清：

```text
全部实体
活跃实体
脏实体
可见实体
同步实体
```

推荐管线：

```text
Input / Trigger
  -> 激活实体或区域
  -> ActiveSet tick
  -> DirtyJournal 记录变化
  -> AOI/chunk filter
  -> per observer WorldDelta
```

静态、休眠、AOI 外且无规则触发的实体不应每 tick 运行复杂规则。

必须持续观察的指标：

```text
server tick ms
active entity count
dirty entity count
observer delta entity count
delta bytes per observer
presentation fact count
input pending count
input correction count
prediction replay count
client visible view count
client active animation count
interpolation buffer delay
```

不能承诺永远没有瓶颈。正确目标是瓶颈出现时能定位、能裁剪、能降级。

## 远端实体和复杂事件

远端玩家、怪物和普通动态实体不做客户端预测，使用 snapshot interpolation。

推荐语义：

```text
客户端渲染远端实体时落后服务端 100-150ms
缺包时保持平滑
收到新快照后插值到新轨道
旧快照不倒退应用
```

复杂事件使用 `PresentationFact`：

```text
服务端结算权威 WorldDelta
服务端附带 PresentationFact 解释这次变化
客户端根据 fact 生成表现计划
表现结束后对齐同一个 WorldDelta 的最终状态
```

rotate-pivot / connected body 的表现事实必须表达完整成员关系。客户端不从本地 port graph 或规则补齐服务端没有给的成员。

这类播放组只影响表现，不写权威镜像。

## 和当前 PresentationFact 变更的关系

当前 `refactor-presentation-facts-playback-groups` 已经在做：

```text
WorldDelta 携带结构化 PresentationFacts
服务端只发送事实，不发送 Unity cue
客户端把 fact 转成播放计划
rotate-pivot 使用临时 group playback
播放完成后对齐服务端 snapshot
```

这条线是正确的，但它不等于输入预测/回滚。该变更明确不实现客户端预测、状态历史或输入重放。

后续预测能力应作为新的 OpenSpec 变更独立拆分，不能塞进 PresentationFact 重构。

## 测试路径

### Unity TestFramework

后续 OpenSpec 实现时，至少需要以下 EditMode 测试：

```text
PredictedLocalOverlay 不写 ClientMapWorld 权威状态
收到服务端 ack 后清理已确认 input
从服务端权威玩家状态重放未确认 input
服务端拒绝输入时 overlay 收敛到正确预测结果
WorldDelta 旧 sequence 不覆盖新状态
EntityDelta 当前字段值可在丢中间包后恢复状态
PresentationFact factId 重复到达不会重复创建播放组
延迟补偿按 client press time 映射到 server beat window
过旧、过远未来、重复 input 被拒绝或降级
同一 action window 只消费一个主动作
```

### Shared / Server 验证

Shared 层变更优先验证：

```text
dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore
```

服务端规则变更优先验证：

```text
dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore
```

OpenSpec 变更必须验证：

```text
openspec validate <change-id> --strict --no-interactive
```

### 手动端到端验证

用户手动验证路径：

```text
启动 Fantasy 服务端
启动 Unity Play Mode 客户端 A
启动第二个 Unity 客户端 B
两个客户端进入同一权威世界
给 A 模拟 50ms / 100ms / 200ms 延迟和少量抖动
```

必须观察：

```text
A 按键后立即有响应
A 普通移动成功时最终坐标与服务端一致
A 被服务端拒绝时不会污染 ClientMapWorld
A pending input 不会永久残留
B 看到 A 平滑移动，但允许略微延迟
旋转组、连接体和复杂事件最终状态一致
AOI 外实体不会被客户端全量创建和更新
```

## 后续 OpenSpec 拆分建议

不要一次性做完整网络同步大重构。建议拆成可验证的纵向切片：

```text
1. add-local-prediction-overlay
   只做本地玩家 overlay、input history、ack 清理、重放和校正，不碰 AOI。

2. add-rhythm-input-lag-compensation
   输入包时间戳、server beat window 映射、最大补偿窗口、重复/过旧/未来输入处理。

3. update-worlddelta-sequencing-resync
   serverTick/sequence、旧包丢弃、lastProcessedInputId 冗余、resync 入口。

4. add-aoi-chunk-delta-sync
   per observer chunk radius、关系订阅、fact member 补齐、delta bytes 统计。

5. add-authoritative-sync-metrics
   server tick ms、active/dirty/visible/delta bytes/prediction correction 指标。
```

每个变更都必须有 Unity TestFramework 或 Shared/Server 自动化测试，并给出手动端到端验证步骤。

## 仍需定参数

这些不是架构分歧，但实现前需要定默认值：

```text
server tick rate
action window 时长
Perfect / Good / Failed 窗口
MaxLagCompensationMs
MaxFutureInputMs
最大 pending input 数
最大预测重放 tick 数
远端插值 buffer 时长
chunk AOI radius
关系订阅生命周期
full snapshot / resync 触发条件
```

参数必须能在测试和调试面板中观察，不要只写死在协议或表现层里。

