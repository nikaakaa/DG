# Design: 统一输入意图层

## Context

当前链路已经实现普通玩家输入按 `entityId + beatTick` 在服务端收口，并通过服务端权威 action pipeline 结算移动。但客户端正式输入仍接近 demo 驱动，协议和运行时入口仍主要表达方向移动。现有实现把客户端声明 `BeatTick` 和服务端消费窗口混在一起，导致快速连续请求可能被服务端分配到连续未来 tick，而不是在同一个服务端收集窗口内互相覆盖。

目标不是做完整节奏判定，也不是做客户端全局预测或全局 rollback。目标是先建立一个不懂玩法规则的输入抽象层，让玩家、AI、Replay、Debug、Script 等来源都能统一表达输入事实。

## Goals

- 输入层输出统一 `InputIntent`，不直接输出最终坐标或规则结果。
- 客户端普通玩家输入使用 Unity Input System。
- 服务端验证客户端声明型意图，再承认为服务端输入事实。
- 输入授权读取最终组件事实，支持 `PlayerControlComponent` 以及后续 RuntimeEffect / 状态对控制权的改变。
- 保持现有服务端权威 action pipeline 和 `WorldDelta` 收敛模型。
- 第一阶段只把普通方向移动接入 `InputIntent`，不实现真实节奏拍点计算、攻击、技能或 AOI。

## Non-Goals

- 不做全局 rollback。
- 不让客户端 `ClientMapWorld` 裁决权威坐标。
- 不在输入层判断碰撞、推动、机关、技能冷却或目标合法性。
- 不把所有 entity 行为塞进玩家输入队列。
- 不在本变更实现完整 AOI，只为 AOI 保留 `TargetHint` 和服务端重新解析目标的边界。
- 不在本变更实现真实 `Perfect / Good / Late / Miss` 判定；字段可保留，默认 `None`。

## Decisions

### Decision: InputIntent 是输入事实

`InputIntent` 表示“某个来源在某个时间或拍点，对某个 actor 提交了某类输入意图”。它可以包含：

```text
IntentId
SourceKind
ActorEntityId
InputKind
Direction
TargetHint
BeatTick
RhythmJudge
ClientInputId
ClientTick
SampleTimeMs
CreatedServerTick
```

它不包含：

```text
ActionSpec 静态策略
碰撞结果
推动结果
机关结果
冷却结果
最终坐标
WorldDelta 数据
```

### Decision: 客户端声明，服务端承认

客户端可以生成 `InputIntent` 以支持本地 pending、即时反馈和统一协议语言。但客户端提交的是声明，不是权威事实。

服务端收到后必须：

```text
校验来源
校验 session 与 actor 控制关系
校验 inputId 去重
校验服务端输入窗口
应用限流
承认为 ServerInputIntent 或拒绝
```

### Decision: 授权与行动结果分离

`IntentAuthorization` 只回答“这个来源此刻是否有资格提交这个 actor 的意图”。行动是否成功由后续 action pipeline 裁决。

示例：

```text
授权通过，但右侧是墙，Move action 失败。
授权通过，但目标超出攻击范围，Attack action 失败。
授权失败，则意图不进入 action pipeline。
```

### Decision: 控制权读取最终组件事实

`PlayerControlComponent` 表示基础玩家归属。后续 RuntimeEffect 或状态层可以临时禁用、转移、增加或限制控制权。授权层读取最终组件/状态事实，而不是只读静态组件。

### Decision: TargetHint 只是提示

`TargetHint` 可用于表达点击、瞄准、锁定或交互对象。服务端 Targeting 必须基于权威 `GameWorld` 重新解析目标。客户端 AOI 镜像中的目标提示不能替代服务端目标裁决。

### Decision: 第一阶段合并策略保守

第一阶段只要求：

```text
同 actor + 同服务端消费窗口 + 同输入通道
普通方向 Move 只保留最后一个
Debug 不参与普通输入合并
不同 InputKind 不做复杂优先级
```

客户端声明 `BeatTick` 是节奏参考字段，不能决定服务端普通输入消费窗口。服务端必须在收到普通玩家输入时分配 `ConsumeTick` 或等价窗口 id，并以该服务端窗口作为缓冲 key。没有明确允许 input-ahead 的情况下，快速连续输入不得自动排入多个未来 tick。

攻击、互动、技能、组合输入、动作优先级以后单独 OpenSpec。

### Decision: IntentAdapter 连接现有 action pipeline

`InputIntentBuffer` 消费后的意图交给 `IntentAdapter`。适配层负责把 `Move(direction)` 转成现有 player movement action。后续 `Attack`、`Interact`、`Skill` 也从这里映射到明确 `ActionSpecId` 或等价数据入口。

## Risks / Trade-offs

- 统一模型会比 Direction-only 多一层数据结构。缓解：第一阶段只接普通 Move，不扩张玩法。
- 客户端生成 `InputIntent` 会带来客户端声明字段可信度问题。缓解：服务端只信授权和归一化后的字段。
- RuntimeEffect 控制权还未完整接入。缓解：本变更只定义授权读取最终事实的边界，具体 effect 种类后续落地。
- Unity Input System 引入会影响现有 demo 输入脚本。缓解：保留旧 demo 作为迁移期或调试路径，但正式入口不能依赖旧 Input API。

## Verification

- Unity TestFramework EditMode:
  - Input System 输入能生成 Move `InputIntent`。
  - pending intent 不修改 `ClientMapWorld.CoreWorld` 权威坐标。
  - 失败状态能清理 pending intent。
- Server verification:
  - 同 actor 同 tick 多个 Move intent 只保留最后一个。
  - 不同 actor intent 互不覆盖。
  - 未授权 actor 被拒绝。
  - 授权通过但规则失败仍由 action pipeline 返回失败。
- Manual:
  - 启动服务端和两个 Unity 客户端。
  - 客户端 A 快速提交多个方向，服务端只结算最终方向。
  - 客户端 A 和 B 最终通过同一 WorldDelta / 可见 delta 收敛。
