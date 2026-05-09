# Subject 与连接体边界

## 文档目的

这份文档只解决一个问题：

```text
动作命中某个 entity 时，本次结算到底作用于谁。
```

它不重做完整事务系统，也不要求现在实现 `CompositeEntity`。当前规则层已经有 `ActionRequest`、`ActionSpec`、`BodyResolver`、`MovePlan`、`CommitProposal` 和 `PendingActionState`，缺口主要是 subject 解析边界。

## 核心结论

短期使用 `ConnectedBodyView` 解决连接体结算。

长期保留 `CompositeEntity`，但只用于有整体身份、整体状态和 member 生命周期管理的对象。

第一阶段不要实现 `CompositeEntity`，也不要把它放进普通 port 连接的默认路径。当前阶段只收紧动作命中入口到本次结算 subject 的解析边界。

最终方向是混合模型：

```text
Subject
  SingleEntity
  ConnectedBodyView
  CompositeEntity
```

这意味着后续会同时存在两种多 entity 表示，但它们不是竞争关系：

```text
ConnectedBodyView = 结算时怎么看一组 entity
CompositeEntity = 世界里是否存在一个长期组合对象
```

不要把所有 port 临时连接都默认升级成 `CompositeEntity`。

## 术语边界

```text
Source
  谁发起动作，例如 Player / Mechanism / Auto / Runtime / Debug。

Entry
  动作命中的入口 entity，例如传送带格子上的 A。

Target
  动作目标，例如目标坐标、方向、blocker、被命中的 entity。

Subject
  本次结算真正作用的对象，可以是单体、临时连接体、持久组合体或实体集合。
```

示例：

```text
传送带命中 A
A 与 B 通过 port 连接成 [A-B]

Entry = A
Subject = [A-B]
```

不能把 `Subject` 简单叫成 `Target`，因为当前系统里 `Target` 已经承担目标坐标、方向、blocker 等语义。

## 方案 A：ConnectedBodyView

`ConnectedBodyView` 是结算时从当前世界状态临时解析出来的连接体。

```text
Entry = A
BodyResolver(A) -> [A-B]
Subject = ConnectedBodyView([A-B])
```

当前代码里的对应实现名是 `BehaviorBody` / `BehaviorBodyKind.PortConnected`。文档里的 `ConnectedBodyView` 表达的是职责语义，不要求第一阶段立刻改名。

特点：

```text
不写入 GameWorld
不持久
不管理生命周期
不保存整体血量、能量、owner、库存
每次结算按当前 port / position / direction 重新解析
```

适合：

```text
普通 port 连接
临时拼接
只要求整体移动、整体阻塞、整体 handoff
传送带不能拆开 [A-B] 这类问题
```

不适合：

```text
车辆
机器
多格怪物
玩家建造结构
有整体生命周期和整体状态的装置
```

评价：

```text
短期最稳，应先用它修正连接体结算。
```

## 方案 B：CompositeEntity

`CompositeEntity` 是世界中真实存在的持久组合体。

```text
CompositeEntity 1000
  members: A, B, C
  health
  energy
  owner
  controller
```

适合：

```text
车辆
机器
多格怪物
玩家建造结构
有整体生命周期的装置
需要整体血量、能量、控制权、阵营、库存的对象
```

代价：

```text
需要维护 member 加入和离开
需要处理拆分、合并、销毁
需要定义 composite component 与 member component 的关系
需要处理 spatial / dirty / snapshot / delta
需要状态机管理 tag 和 component 生命周期
```

评价：

```text
长期需要，但不适合作为所有连接体的默认表达。
```

## CompositeEntity 状态机

如果 `CompositeEntity` 要管理子 entity 的 tag / component 生命周期，它必须是有状态机的世界对象，而不是简单 subject wrapper。

建议状态：

```text
Assembling
Active
Reconfiguring
Splitting
Destroyed
```

状态机负责：

```text
member 什么时候加入
member 什么时候离开
member 离开时清理哪些 tag / component
拆分时哪些状态下放到 member
销毁时 member 是否一起销毁
重构时是否暂停移动、推动、交互
```

这部分不属于 `ConnectedBodyView`。

## Component / Tag 生命周期边界

`CompositeEntity` 给 member 带来的 tag / component 应该有明确来源。

推荐模型：

```text
static source
runtime effect source
composite source
  -> ComponentStateResolver
  -> final component / tag result
```

不要让 `CompositeEntity` 直接覆盖或删除 member 的静态 component。

拆分时应只移除 composite source，保留 member 自己的 static source 和 runtime effect source。

## 事件驱动边界

未来如果做事件驱动，`CompositeEntity` 可以响应事件：

```text
MemberAdded
MemberRemoved
MemberDestroyed
PortChanged
TagChanged
ComponentResultChanged
SettlementCommitted
SettlementRejected
```

但事件处理不应绕过事务系统直接改最终世界状态。

推荐路径：

```text
事件进入 CompositeEntity 状态机
状态机产出 SettlementRequest 或 component result source 变化
统一进入结算或 ComponentStateResolver
```

## 方案 C：混合模型

推荐最终模型是：

```text
默认 port 连接 -> ConnectedBodyView
需要长期身份 -> CompositeEntity
```

两者职责不同：

```text
ConnectedBodyView
  临时结算视图
  回答“这次 action 要把哪些 entity 当整体处理”
  不负责生命周期
  不保存整体状态
  不管理 member tag / component

CompositeEntity
  持久世界对象
  回答“这个组合体本身是不是一个长期存在的对象”
  负责成员关系
  负责状态机
  可以管理整体状态和 member 生命周期
```

Subject 解析顺序建议：

```text
Entry entity
  -> 如果属于 CompositeEntity，Subject = CompositeEntity（未来阶段）
  -> 否则如果 ActionSpec 允许 connected body 且 port 连接出多个 member，Subject = ConnectedBodyView
  -> 否则 Subject = SingleEntity
```

需要后续单独确认：

```text
CompositeEntity 是否允许再通过 port 与外部形成临时 ConnectedBodyView
CompositeEntity 与外部连接时 subject 是 composite 本体，还是 composite + 外部 view
```

## 传送带处理原则

传送带不应该直接移动 entity。它只负责产生机制推动请求。

正确流程：

```text
PushOnEnter 扫描到 A
生成一个 move-like ActionSpecId 请求
通过 ActionSpecId 查找 ActionSpec
SubjectResolver 解析 A
读取 ActionSpec.subjectPolicy
如果 subjectPolicy = ConnectedBodyIfAny 且 A 属于 [A-B]，Subject = [A-B]
如果 subjectPolicy = HitEntity，Subject = A
RulePlanner 按 [A-B] 生成整体 MovePlan
ConflictResolver 整体提交或整体拒绝
如果被 [C-D] 阻挡，再进入 handoff
```

收敛目标是：

```text
ActionSpecId
  -> ActionSpec
    -> subjectPolicy = HitEntity / ConnectedBodyIfAny
```

`mechanism_push`、`connected_body_move` 等字符串只能是 `ActionSpecId`，只用于查配置。规则层不得根据这些名字分支决定 subject。

第一阶段目标不是继续扩展更多 `connected_body_xxx` action，而是把 subject 选择收敛到 `ActionSpec.subjectPolicy`。`connected_body_move` 作为历史兼容 action 可以暂时存在，但不能代表新的扩展方式。

## ActionSpec 最小改动方向

当前 `connected_body_move` 存在，是历史上用 action 名区分 connected body subject 的遗留结果。

当前代码已有 `ActionSubjectKind.SingleEntity / ConnectedBody`。更好的方向不是新增一套平行枚举，而是把现有字段升级或重命名为更明确的 subject policy：

```text
SubjectPolicy
  HitEntity
  ConnectedBodyIfAny
  CompositeEntityIfAny（未来保留，不进入第一阶段）
```

示例：

```text
some ActionSpecId -> HitEntity
some ActionSpecId -> ConnectedBodyIfAny
```

哪些现有 `ActionSpecId` 使用 `ConnectedBodyIfAny` 需要按玩法语义逐条确认。`player_push` 是否使用 `ConnectedBodyIfAny` 也需要单独确认。

第一阶段最小变更：

```text
ActionSubjectKind.SingleEntity -> HitEntity 语义
ActionSubjectKind.ConnectedBody -> ConnectedBodyIfAny 语义
需要 connected body 结算的现有 ActionSpec 配置支持 ConnectedBodyIfAny
规则层不根据 ActionSpecId 名字决定 subject
CompositeEntityIfAny 只写入未来边界，不实现
```

`SubjectResolver` 只负责从 entry entity 和 subject policy 得出本次 subject。它不生成 `MovePlan`，不判断 blocker，不创建 handoff，也不修改 `GameWorld`。

## 测试要求

必须覆盖：

```text
BodyResolver 从任意 member 都能解析同一个 connected body
传送带命中 connected body 任意 member 时不会单独移动该 member
connected body 目标为空时整体移动
connected body 目标被 blocker 阻挡时整体不动
connected body 被另一个 connected body 阻挡时走 handoff，且不拆分 member
普通 non-port entity 不受 connected body subject policy 影响
```

测试只需要使用 Unity 自带 TestFramework 的 EditMode 测试覆盖规则层行为。端到端验证由用户手动在 Play Mode / 双端环境确认。

## 停止条件

出现以下实现方向时应停止：

```text
传送带直接调用 MoveEntity 移动多个 member
每种来源都新增一个 connected_body_xxx action
CompositeEntity 被用于所有普通 port 连接
SubjectResolver 同时修改世界状态
客户端表现层参与服务端 subject 判定
```
