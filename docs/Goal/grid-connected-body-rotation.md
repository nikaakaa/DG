# 格子连接体旋转边界

## 文档目的

这份文档记录 DG 的连接体模型与机械动力 `Contraption` 模型的边界，并明确后续如果实现连接体旋转，应该补哪些系统。

核心问题是：

```text
DG 的每个 entity 是否一定在格子上？
```

答案是：一定在格子上。

因此 DG 不应该照搬机械动力那种把结构从世界移除、组装成独立运动实体、持续旋转、停止后再放回世界的 `ContraptionEntity` 模型。

DG 要做的是格子结算连接体，不是连续运动结构实体。

## 核心结论

DG 的连接体应该保持为结算视图：

```text
entity 永远在 GameWorld 的格子里
连接体由当前 tick / 当前动作临时解析
移动、推挤、旋转都是一组格子状态的原子变化
结算完成后，每个成员仍然是普通格子 entity
```

机械动力的结构模型是：

```text
方块在世界格子里
assemble 后从世界移除
变成移动中的 contraption entity
可以处于两个格子之间或连续角度
停止后 disassemble 回世界格子
```

这不是 DG 当前目标。

## DG 当前 ConnectedBody 的定位

当前连接体是：

```text
PortConnectorComponent
DirectionComponent
PositionComponent
  -> PortConnectionSystem.CollectConnectedGroup
  -> BodyResolver
  -> BehaviorBody
```

它的职责是：

```text
找出本次动作真正影响哪些 entity
给移动、推挤、旋转规划提供成员列表
给占位校验提供内部旧格集合
给提交层提供原子提交范围
```

它不负责：

```text
离开格子
持续角度
中间态碰撞
独立生命周期
assemble / disassemble
作为世界里的长期组合实体
```

因此更准确的名字是：

```text
ConnectedBodyView
GridConnectedBody
ActionBody
SettlementBody
```

当前代码里的 `BehaviorBody` / `BehaviorBodyKind.PortConnected` 是实现名，语义上对应 `ConnectedBodyView`。

## 与机械动力的差异

机械动力的 `Contraption` 比 DG 当前连接体多出的是生命周期系统，而不是单纯连接算法。

它大致有这些层：

```text
driver block
assemble
local block map
anchor
continuous transform
contraption entity
collision / stalled
movement actors
disassemble
```

DG 不需要这些层作为连接体的默认路径。

DG 需要的是：

```text
driver entity
body resolve
grid transform
occupancy validation
atomic commit
snapshot delta
```

也就是说，机械动力解决的是“世界里有一个运动中的结构实体”，DG 解决的是“这次格子结算作用于一组 entity”。

## 连接体旋转的正确语义

DG 的完整连接体旋转应该是离散动作：

```text
当前连接体布局
  -> 围绕 pivot 计算目标布局
  -> 校验目标格
  -> 一次性提交所有成员的新坐标和新方向
  -> 所有成员仍然在格子上
```

示例：

```text
旋转前：

A B
  C

以 A 为 pivot 顺时针旋转后：

A
C B
```

这里没有：

```text
37 度中间态
73 度中间态
旋转中途占位
结构实体
脱离 GameWorld
disassemble
```

客户端可以播放从旧格子到新格子的旋转动画，但那只是表现层。服务端权威状态只记录结算后的格子结果。

## 第一版不应引入的东西

第一版连接体旋转不要引入：

```text
ContraptionEntity
CompositeEntity 默认路径
持续 angle 权威状态
assemble / disassemble
中间态物理碰撞
移动结构 actor tick
独立结构库存或能量
```

这些属于更大的机械系统或长期组合实体系统。

## 后续需要补的系统

### 1. RotatableComponent

表示 entity 是否允许参与旋转。

它只回答能力问题：

```text
这个 entity 能不能被旋转
```

它不决定：

```text
谁驱动旋转
绕哪里旋转
旋转后是否合法
```

### 2. RotationDriverComponent

表示某个格子 entity 是旋转驱动器。

它用于回答：

```text
谁发起旋转
从哪个方向找到被旋转连接体
默认旋转方向是什么
旋转 cost 是多少
```

类似机械动力的轴承，但它仍然是一个格子 entity，不会把结构组装成独立实体。

### 3. RotationPivot

明确绕哪个格子旋转。

第一版建议使用最简单规则：

```text
debug 旋转：以被选中 entity 的坐标为 pivot
driver 旋转：以 RotationDriverComponent 所在坐标为 pivot，或以 driver 前方 root 坐标为 pivot
```

不要一开始支持复杂 pivot 策略。

### 4. GridRotationTransform

补一个只处理格子离散旋转的变换层。

它负责：

```text
GridCoord 绕 pivot 旋转 90 度
Direction 旋转 90 度
DirectionMask 旋转 90 度
```

当前已有方向和端口旋转能力，但还缺坐标绕 pivot 的旋转。

2D 第一版只需要：

```text
clockwise offset: (x, y) -> (y, -x)
counterclockwise offset: (x, y) -> (-y, x)
```

### 5. RotatePlan

类似 `MovePlan`，但不要伪装成移动。

建议结构：

```text
RotatePlan
  SourceActionId
  SourceStateId
  BodyId
  Pivot
  Direction
  Members

RotateMember
  EntityId
  FromCoord
  ToCoord
  FromDirection
  ToDirection
```

旋转规划必须一次性算出所有成员的新格子和新方向。

### 6. RotationOccupancyResolver

旋转不能直接复用普通移动校验。

它需要允许：

```text
成员落回连接体自己的旧格子
```

它需要拒绝：

```text
目标格被外部 blocker 占用
目标格被 player 占用
两个成员旋转后落到同一格
成员缺 PositionComponent
成员缺 DirectionComponent
成员不允许旋转
```

### 7. RotateBodyCommit

旋转必须原子提交。

不能逐个成员先 `MoveEntity` 再 `SetDirection`，否则中途失败会出现半提交。

建议新增：

```text
CommitProposalKind.RotateBody
```

或新增提交组语义，让一组位置和方向变化同成同败。

第一版更推荐 `RotateBody`，因为语义更清楚。

### 8. 快照与客户端表现

服务端提交后只需要发成员的新：

```text
Position
Direction
ServerTick
```

现有 `EntitySnapshot` 已经有坐标和方向，客户端镜像理论上可以复用。

后续如果要动画，可以在 `ClientWorldVisuals` 做表现插值，但不能改变服务端权威状态。

## 与 CompositeEntity 的关系

连接体旋转第一版不需要 `CompositeEntity`。

只有当目标变成下面这些，才需要考虑 `CompositeEntity`：

```text
整体生命值
整体能量
整体库存
整体 owner
member 加入和离开生命周期
拆分和合并
长期机器状态机
```

普通 port 连接仍然应该走：

```text
PortConnectionSystem
  -> ConnectedBodyView
  -> RotatePlan / MovePlan / PushHandoff
```

不要把所有普通 port 连接默认升级成持久组合体。

## 与机械动力可借鉴的部分

可以借鉴：

```text
driver 发起结构动作
anchor / pivot 明确化
local offset 到 world coord 的变换
结构级原子校验
blocked / stalled 结果可观察
移动结构上的 actor 行为作为未来扩展
```

不要借鉴到第一版：

```text
方块脱离世界
连续角度作为权威状态
独立 contraption entity
中间态物理碰撞
assemble / disassemble 生命周期
```

## 推荐实现顺序

```text
1. 补 RotatableComponent
2. 补单 entity rotate action，只改 DirectionComponent
3. 补 Direction / DirectionMask / Port 旋转测试
4. 补 GridRotationTransform
5. 补 RotatePlan 和 RotateMember
6. 补 RotationOccupancyResolver
7. 补 RotateBodyCommit 原子提交
8. 接 debug rotate 入口
9. 接正式 RotationDriverComponent
10. 视需要补客户端旋转表现动画
```

这个顺序保证先把小能力测稳，再扩展到连接体整体旋转。

## Unity Test Framework 测试要求

实现时至少需要以下测试：

```text
单 entity rotate 成功改变 DirectionComponent
没有 RotatableComponent 的 entity rotate 被拒绝
port entity rotate 后 world ports 改变
两个 port 原本连接，旋转后断开
两个 port 原本不连接，旋转后连接
连接体整体旋转后所有成员坐标正确
连接体整体旋转后所有成员方向正确
连接体旋转允许占用自身旧格
连接体旋转遇到外部 blocker 整体失败
连接体旋转遇到 player 整体失败
连接体旋转目标格重复整体失败
失败后所有成员坐标和方向保持不变
rotate 与 move 同 tick 命中同一成员时按优先级或冲突规则只提交一个
```

测试只使用 Unity 自带 Test Framework。

不要用 Unity build 作为验证入口。

## 手动端到端验证

用户手动验证时建议覆盖：

```text
1. 在 Play Mode 用 debug 面板生成两个 port entity，让它们连接。
2. 选中其中一个执行 rotate，确认连接断开或重连符合预期。
3. 生成三格以上连接体，执行整体 rotate，确认所有成员落到目标格。
4. 在目标格放 blocker，确认旋转失败且没有半提交。
5. 两个客户端观察同一旋转，确认服务端结果同步到观察端。
```

验证状态要分层报告：

```text
Unity EditMode tests passed
server authoritative rotate accepted
client mirror received snapshots
debug panel operation usable
manual dual-client observation passed
```

不能把其中任意一层成功说成完整完成。

## 最终边界

DG 的连接体旋转不是机械动力式 `Contraption`。

它的最终形式应该是：

```text
格子 entity
  -> 临时解析 connected body
  -> 离散 grid transform
  -> 原子 rotate commit
  -> 仍然是格子 entity
```

这条边界比机械动力更窄，也更适合 DG 当前的权威格子结算模型。
