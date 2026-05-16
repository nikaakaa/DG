# 节奏地牢输入意图层产品目标

## 定位

这份文档描述 DG 作为节奏地牢最终应该拥有的输入意图抽象。它不是 OpenSpec 提案，也不是实现任务列表。后续如果要落地其中某一块能力，需要再走 OpenSpec，把能力拆成可测试的变更。

DG 的输入层不应该直接知道移动、攻击、碰撞、推动、机关、技能冷却或最终坐标。它应该先抽象出一层纯输入意图，成为玩家、节拍、设备、网络、调试、录像和后续规则系统之间的稳定边界。

最终目标是：

```text
任何输入来源都先变成 InputIntent
InputIntent 不知道玩法规则
规则系统只消费稳定的意图事实
客户端可以基于意图做即时反馈
服务端可以基于意图做权威裁决
万级 entity 的世界推进不被玩家输入层拖垮
```

## 核心抽象

输入层最终只输出一种稳定语言：

```text
InputIntent
```

`InputIntent` 表示一个输入事实：

```text
谁
在什么时间或哪一拍
从什么来源
提交了什么输入意图
输入质量如何
```

它不表示规则结论：

```text
能不能移动
能不能攻击
目标是否合法
有没有撞墙
会不会推动箱子
技能有没有冷却
最终坐标是什么
```

这些问题属于后续 `Intent Adapter`、`ActionRequest`、`Action Pipeline` 和服务端权威规则层。

推荐边界是：

```text
Raw Input
  键盘 / 手柄 / 触屏 / AI / Debug / Replay / Script

Input Intent Layer
  采样、归拍、判定、合并、去重、排序、缓存、过期

Intent Adapter
  把 InputIntent 翻译成 ActionRequest

Action Pipeline
  目标选择、条件检查、仲裁、计划、提交、效果

WorldDelta
  广播权威结果，客户端收敛
```

一句话：

```text
InputIntent 是输入事实
ActionRequest 是规则请求
WorldAction 是服务端执行单元
WorldDelta 是权威结果
```

## InputIntent 应该包含什么

产品层需要的最小语义是：

```text
IntentId
SourceKind
ActorEntityId
BeatTick
InputKind
Direction
TargetHint
RhythmJudge
ClientInputId
ClientTick
SampleTimeMs
CreatedServerTick
```

字段含义：

```text
IntentId
输入意图唯一标识，用于去重、追踪、调试和录像。

SourceKind
输入来源，例如 Player、AI、Debug、Replay、Script。

ActorEntityId
这次输入意图作用的实体。输入层只记录谁，不判断这个实体是否真的能行动。

BeatTick
输入归属的目标拍。非节奏输入也可以使用当前逻辑 tick 或特殊拍。

InputKind
输入类型，例如 Move、Attack、Interact、Wait、Skill、Cancel。

Direction
方向参数。不是所有输入都需要方向。

TargetHint
目标提示，可以是格子、实体、方向范围或空。输入层不验证目标是否合法。

RhythmJudge
节奏判定，例如 Perfect、Good、Early、Late、Miss、None。

ClientInputId
客户端输入序号，用于 ack、重发、去重和调试。

ClientTick
客户端本地 tick，用于追踪输入发生时的本地状态。

SampleTimeMs
原始采样时间，用于判定窗口、offset 调试和录像。

CreatedServerTick
服务端接收或创建该意图时的 tick。
```

`InputIntent` 不应该携带：

```text
ActionSpec 的静态策略
碰撞结果
推动结果
冷却结果
最终坐标
伤害结果
机关触发结果
WorldDelta 数据
```

## 输入意图类型

最终普通输入不应该被 `Direction` 锁死。`Direction` 只是某些意图的参数。

基础意图类型：

```text
Move(direction)
Wait
Attack(direction or target)
Interact(direction or target)
Dash(direction)
Skill(skillId, direction or target)
Cancel
Turn(direction)
```

非玩家来源也可以产出同一套意图：

```text
AI 产出 AttackIntent
Replay 产出 MoveIntent
Debug 产出 SpawnIntent 或 MoveIntent
Script 产出 InteractIntent
```

这样后续系统不需要关心输入来自键盘、手柄、AI、录像还是调试工具，只需要消费统一的输入事实。

## 这一层不应该知道什么

Input Intent Layer 不依赖：

```text
GameWorld 规则裁决
ActionSpec 策略
碰撞系统
推动系统
机关系统
技能冷却
RuntimeEffect
目标选择规则
Fantasy Session
Unity 表现对象
```

它可以知道：

```text
输入设备
输入来源
采样时间
节拍时间
判定窗口
输入合并策略
去重策略
过期策略
排序策略
```

边界必须清楚：输入层只产出事实，不产出规则结果。

## 玩家最终会感受到什么

玩家进入地牢后，核心体验不是“按键后等待服务器移动”，而是：

```text
听到节拍
提前或准点输入
立刻看到方向、命中、起手反馈
到拍点时角色与世界一起结算
如果输入偏早或偏晚，系统给出清晰判定
如果网络波动，手感反馈仍然稳定，最终位置由服务端收敛
```

移动、攻击、互动、技能释放都应该围绕拍点发生。玩家可以理解每次操作属于哪一拍，也能感受到自己是否命中节奏窗口。

输入体验应该支持三种层次：

```text
节奏感
按键被节拍吸附，Perfect / Good / Late / Miss 有明确反馈

动作感
按下瞬间有方向、高亮、起手动画、音效或 UI 反馈

权威感
最终落点、碰撞、推动、机关触发、多人成果由服务端统一裁决
```

## 产品上最终会有什么

### 节拍行动

玩家的普通行动以拍为单位结算。一次输入不是“现在我要移动”，而是“我在某一拍提交了一个行动意图”。

行动可以是：

```text
移动
等待
攻击
互动
冲刺
释放技能
转向
取消或防御
```

方向只是输入意图的一个参数，不是输入层的全部。

### 节奏判定

输入层需要给每次玩家输入一个节奏判定：

```text
Perfect
Good
Early
Late
Miss
Invalid
```

这些判定服务于产品体验：

```text
Perfect 可以触发更强反馈、连击、额外收益
Good 可以正常行动
Early 可以进入输入缓冲，吸附到目标拍
Late 可以根据窗口宽容度结算或失败
Miss 可以触发空拍、破连、硬直或惩罚
Invalid 表示不合法输入，例如沉默、眩晕、无权限、动作不可用
```

节奏判定不应该和网络队列状态混在一起。网络层的 `Buffered / Replaced / Consumed / Expired / Rejected / Resolved` 只说明输入生命周期，不说明玩家打拍质量。

### 输入缓冲

节奏地牢必须允许合理的提前输入。玩家在拍点前短时间按下方向或动作，系统应该能把它缓冲到目标拍，而不是因为没有精确按在某一帧就丢掉。

产品规则应该明确：

```text
提前窗口多长
延后窗口多长
同一拍多输入怎么处理
按住方向是否持续生成意图
攻击和移动同拍时谁优先
是否允许斜向或组合输入
Miss 后是否吞掉后续输入
```

当前服务端已经有同一实体同一拍只保留最终输入的基础，但产品层还需要定义“最终输入”到底按最后一次、优先级、动作类型，还是判定质量来选择。

### 即时反馈

玩家按键后，客户端必须立刻反馈，不等服务端最终结算。

即时反馈包括：

```text
节拍命中 UI
目标格高亮
方向箭头
角色起手动画
按键音效
技能预备特效
输入已缓存提示
输入失败提示
```

这些反馈是非权威表现，不直接修改权威世界坐标。服务端结果回来后，客户端根据 `WorldDelta` 确认、取消或修正表现。

### 多人一致

多人场景下，每个客户端都可以有本地反馈，但最终世界必须一致。

例如两个玩家同拍抢同一格：

```text
两个客户端都可以立刻显示自己的输入反馈
服务端在拍点统一裁决
最终只有服务端认可的结果进入 WorldDelta
所有客户端收敛到同一个地牢状态
```

输入层不能为了手感牺牲多人一致性。

## 万级 entity 下的产品边界

万级 entity 不代表万级 entity 都走玩家输入层。玩家输入层只处理外部玩家意图。

世界里大量 entity 的行为应该来自：

```text
服务端 tick
机关触发
AI 决策
自动移动
运行时效果
延迟 action 输出
空间索引候选扫描
```

因此最终产品边界是：

```text
玩家输入层只负责玩家意图和节奏手感
世界推进层负责万级 entity 的状态演化
规则层负责行动裁决、碰撞、推动、机关、效果
同步层负责让客户端看到同一个权威结果
```

输入层不能变成“所有 entity 行为的入口”。如果万级 entity 都挤进普通玩家输入队列，架构会变形，产品也会失去稳定性。

## 最终链路

最终链路应该是：

```text
本地采样
  读取键盘、手柄、触屏、自动重复输入

节奏映射
  把输入采样时间映射到 beatTick 和 beatPhase

判定窗口
  得出 Perfect / Good / Early / Late / Miss

意图归一化
  生成 InputIntent，而不是只生成 Direction

本地反馈
  播放非权威 UI、音效、动画、目标提示

网络提交
  发送 inputId、entityId、beatTick、intent、direction、clientTick、判定信息

服务端输入网关
  校验权限、节拍窗口、频率、重复包、非法动作

服务端拍点缓冲
  按 entityId + beatTick 收口为该拍最终意图

权威 action intake
  通过 Intent Adapter 把最终意图转为 ActionRequest / WorldAction

规则裁决
  处理移动、攻击、碰撞、推动、机关、效果、冲突

WorldDelta
  广播最终结果，客户端收敛
```

## 产品能力清单

### 输入设备

最终应该支持：

```text
键盘
手柄
触屏或虚拟摇杆
重绑定
按住方向
单拍输入
组合输入
```

输入设备层只产生原始输入，不知道规则结果。

### 节拍可调

产品需要能调整：

```text
BPM
每拍时长
输入提前窗口
输入延后窗口
判定档位
客户端本地 offset
音频延迟 offset
网络延迟容忍窗口
```

这些参数应该可配置、可测试、可调试，而不是写死在 MonoBehaviour 里。

### 输入意图

最终普通玩家输入应该是输入意图：

```text
Move(direction)
Wait
Attack(direction or target)
Interact(direction or target)
Dash(direction)
Skill(skillId, direction or target)
Cancel
Turn(direction)
```

这样后续玩法不会被 `Direction` 协议锁死。

### 反馈分层

反馈分三类：

```text
输入反馈
按键命中、缓存、Miss、方向提示

动作反馈
起手、挥砍、蓄力、脚步、预备姿态

权威反馈
落点、受击、死亡、机关触发、推动链、掉落、同步修正
```

输入反馈可以立即出现，权威反馈必须来自服务端结果。

### 调试和观测

节奏输入必须有调试面板，否则很难调手感。

至少需要观察：

```text
clientInputId
entityId
intentKind
direction
clientBeatTick
serverBeatTick
beatPhase
judgeOffsetMs
judgeStatus
queueStatus
roundTripMs
resolvedServerTick
finalCoord
rejectReason
```

这不是锦上添花，是调节奏手感和排查网络体验的必要产品工具。

## 不应该做什么

输入层不应该：

```text
直接修改权威 ClientMapWorld 坐标
替服务端裁决碰撞、推动、机关和伤害
把所有 entity 行为都塞进玩家输入队列
用高频服务端 tick 代替节奏输入设计
把网络状态当成节奏判定
把 Direction 当成所有输入的长期模型
为了手感做全世界 rollback
```

DG 更适合：

```text
客户端即时反馈
服务端拍点裁决
局部表现预测
WorldDelta 收敛
小范围冲突由服务端确定性处理
```

## 产品验收标准

一套完整输入层完成后，应该能用下面这些标准验收。

### 单人手感

```text
玩家按键后 1 帧内有可见或可听反馈
提前输入能被正确缓存到目标拍
准点输入能给出清晰正反馈
晚输入能按规则结算或 Miss
连续按方向不会产生不可解释的多次移动
Miss、无效动作、被控制限制时有清晰反馈
```

### 多人一致性

```text
两个客户端同拍输入后，最终状态一致
抢格、互推、机关触发由服务端统一裁决
客户端本地反馈不会永久污染权威镜像
网络延迟下 pending 输入不会永久残留
重连后旧输入不会重复生效
```

### 大世界稳定性

```text
万级 entity 推进不依赖普通玩家输入队列
玩家输入只影响可控 entity 和相关 action
大量机关和自动 entity 通过系统入口进入 action pipeline
输入网关能限制单 session 和单 entity 的每拍输入量
服务端 tick 不因为玩家快速连按生成无限 action
```

### 可调试性

```text
能看到每次输入的 beat、offset、判定和生命周期
能区分输入被替换、过期、拒绝、已结算
能复现一拍内多输入选择结果
能观察服务端最终裁决和客户端本地反馈的差异
```

## 自动测试方向

后续 OpenSpec 落地时，自动测试只使用 Unity 自带 TestFramework。

建议覆盖：

```text
输入采样时间能映射到正确 beatTick
Early / Perfect / Good / Late / Miss 判定正确
提前输入能进入目标拍缓冲
同拍多输入按产品策略选出最终意图
攻击和移动同拍时按优先级处理
pending 输入收到 Replaced / Expired / Rejected 后清理
本地反馈不会修改权威 ClientMapWorld 坐标
服务端拒绝过旧或过远 beatTick
重复 clientInputId 不会重复生效
同一 session 每拍输入量被限制
WorldDelta 回来后客户端收敛到权威结果
```

## 手动端到端验证

每次输入层关键变更完成后，用户需要手动验证：

```text
启动服务端和一个客户端
按节拍提前、准点、延后输入
观察节奏判定和即时反馈
观察最终移动是否只由服务端结果确认

启动两个客户端
两个玩家同拍抢同一格或相互阻挡
观察两个客户端最终状态是否一致

模拟延迟或快速连按
观察 pending 输入是否能被确认、替换、过期或拒绝
观察客户端是否出现永久错误位置或永久输入残留

制造大量自动 entity 或机关
确认玩家输入手感不因为世界 entity 数量明显恶化
```

## 推荐落地顺序

这不是实现计划，只是产品能力的切片顺序。

```text
1. InputIntent 数据模型和纯输入边界
2. 客户端节奏时钟和输入判定
3. InputIntent 取代 Direction-only 输入模型
4. Intent Adapter 把 InputIntent 转为 ActionRequest
5. 本地非权威输入反馈
6. InputAck 与最终 WorldDelta 语义拆分
7. 服务端输入网关、限流、去重、重发和过期策略
8. 输入调试面板和手感统计
9. 攻击、互动、技能等非移动意图接入
```

第一步不应该做一个很差的临时版本，而是先把最终边界摆正，再按可验证切片落地。

## 结论

DG 的节奏输入层最终应该回答三个问题：

```text
输入来源提交了什么 InputIntent
这个 InputIntent 在时间和节奏上如何归属
后续规则系统如何把它翻译成权威行为请求
```

现在项目已经有服务端拍点缓冲，这是正确基础。但从产品角度看，真正决定节奏地牢手感的是客户端节奏时钟、判定窗口、输入意图、本地反馈、输入生命周期和服务端权威收敛的完整闭环。
