# Rotate Pivot 架构与业务逻辑

## 核心结论

rotate 指的是带唯一旋转点的 connected body 被 push 命中后，按 push 贡献产生绕 pivot 的 90 度刚体旋转响应。

rotate 不是玩家直接输入的 action，也不是普通 move 的方向枚举。它是 push 管线里的特殊 subject response。

最终架构必须满足：

1. 服务端权威决定 rotate 是否触发、何时碰撞、何时回弹、何时提交。
2. `CostTicks` 是逻辑时间，不只是动画时长。
3. rotate 使用 tick 弧段 shape sweep，而不是客户端物理检测，也不是动画推断。
4. connected body 作为整体刚体旋转，任何成员碰撞都会让整个 body bounce。
5. 客户端只消费服务端 fact 和 snapshot，不能本地裁决成功、碰撞、push 或最终坐标。

## 触发条件

rotate 只处理满足以下条件的请求：

- action 是 push 类请求。
- targeting selector 是 `direction_cell`。
- action 允许 connected body subject。
- request direction 不是 `None`。
- blocked result policy 存在 `DeriveAction` 分支。
- 被 push 的实体可以解析出 connected body。
- connected body 中存在 `RotatePivotComponent`。

如果 connected body 没有 pivot，本请求不被 rotate 消费，继续走普通 push / move 管线。

## Pivot 规则

connected body 内的 pivot 数量决定行为：

- 0 个 pivot：不进入 rotate，继续普通 push。
- 1 个 pivot：进入 rotate 响应。
- 多个 pivot：本次 push 无效，不移动，不派生 push，结果原因是 `rotate-pivot-invalid`。

pivot 可以来自静态配置，也可以来自 RuntimeEffect / buff 合成。运行时移除 buff 时，只移除对应 runtime 来源，不能误删静态 pivot 或其他 runtime 来源。

## Push 到 Torque

同一 ready tick、同一 connected body 的 push contribution 先按 body 分组。

分组 key：

```text
ReadyTick + sorted body member ids
```

每个 contribution 根据接触成员相对 pivot 的半径向量和 push 方向计算力矩：

```text
radius = hitMemberCoord - pivotCoord
push = request.Direction
cross = radius.x * push.y - radius.y * push.x
```

规则：

- `cross < 0`：顺时针贡献。
- `cross > 0`：逆时针贡献。
- `cross == 0`：不产生力矩。
- 每个 contribution 按 `DeferredContributionCount` 计数。
- 顺时针贡献大于逆时针贡献时，本 tick 旋转顺时针 90 度。
- 逆时针贡献大于顺时针贡献时，本 tick 旋转逆时针 90 度。
- 两侧贡献相等且存在力矩时，本次 push 被消费但 rotate 无效，结果原因是 `rotate-pivot-zero-torque`。
- 完全没有力矩时，不消费请求，继续普通 push。

push 方向穿过 pivot 时不触发 rotate，应回落到普通 push。

## Connected Body 刚体语义

rotate 的 subject 是整个 connected body，不是单个成员。

规则：

- body 成员之间保持刚体关系。
- pivot 成员坐标不变，但方向和端口表现仍参与整体旋转。
- 非 pivot 成员绕 pivot 旋转 90 度。
- 旋转成功时，所有成员一起提交最终姿态。
- 旋转碰撞时，所有成员一起 bounce 回原权威姿态。
- 不存在某个成员单独绕路、单独成功、单独提交。

## CostTick 弧段 Sweep

rotate 不应该使用固定 90 次细采样再把 progress 量化到 tick。

最终语义是：`CostTicks` 直接切分 90 度。

```text
anglePerTick = 90 / CostTicks

tick 1: 0 -> anglePerTick
tick 2: anglePerTick -> anglePerTick * 2
...
tick N: anglePerTick * (N - 1) -> 90
```

每个 tick 表示一个弧段，而不是一个采样点。

每个 tick 弧段执行 connected body shape sweep。这里的 sweep 不是固定角度采样，而是离散格子的扇形覆盖检测：

```text
bodySweep(tickSegment) = union(memberSweep(member, angleStart, angleEnd))
```

其中每个 member 是单格占用，connected body 是任意形状的多成员组合。

`memberSweep` 的目标是求出该 member 在当前 tick 弧段扫过的所有地图格：

```text
radius = memberCoord - pivotCoord
angleStart = tickIndex * anglePerTick
angleEnd = (tickIndex + 1) * anglePerTick
sector = 扇形(pivotCoord, radius, angleStart, angleEnd, rotateDirection)
memberSweep = all grid cells intersect sector
```

扇形检测按格子做，不按动画帧做：

- pivot 成员不产生位移 sweep，只保留方向 / 端口旋转语义。
- 非 pivot 成员先根据旋转前后位置和扇形外接盒计算候选格集合。
- 对每个候选格，用该格中心或格子方形与扇形范围做相交检测。
- 扇形范围由 pivot、当前成员半径、`angleStart -> angleEnd` 和旋转方向决定。
- 检测必须包含弧段起点后的扫过范围和弧段末端目标格。
- 如果实现选择格子方形相交，必须保证不会漏检；保守多扫可以接受，但不能漏掉 blocker。

推荐实现口径：

```text
for each tickSegment:
  contacts = []
  for each member in rotatingBody:
    sweptCells = SectorCells(pivotCoord, memberCoord, angleStart, angleEnd, rotateDirection)
    for each cell in sweptCells:
      if cell belongs to current rotating body original occupancy:
        continue
      if cell contains external blocking entity:
        contacts.add(contact(member, cell, blocker))

  if contacts is not empty:
    ContactTick = current tick
    collect all contacts in this tick
    stop scanning later ticks
```

规则：

- tick 之间有先后。
- tick 内没有更细的业务先后。
- 每个 tick 弧段必须收集该段弧扫过的所有格子。
- 每个 tick 弧段的末端格也属于该 tick 已扫过范围。
- rotating body 自己当前占用的所有格子被视为可释放空间，必须忽略。
- 扫到外部 blocking entity 时，记录 contact。
- 第一个出现 contact 的 tick 是 `ContactTick`。
- `ContactTick` 内扫到的所有 blocker 都收集。
- 发生 contact 后，不再继续扫描后续 tick 弧段。

不使用 Unity 物理检测做逻辑裁决。Unity 物理最多只能做调试可视化或辅助对照，不能决定 contact、push、bounce、坐标提交。

## 碰撞与 Bounce

rotate 没有碰撞时：

```text
StartTick:
  创建 TimedActionUnit
  输出 RotatePivotGroup Success fact
  锁定 body members
  不提交最终坐标

EndTick:
  提交所有成员最终坐标和方向
  解锁 body members
```

rotate 发生碰撞时：

```text
StartTick:
  创建 TimedActionUnit
  输出 RotatePivotGroup Bounce fact
  锁定 body members
  不提交最终坐标

ContactTick:
  输出 RotatePivotImpact fact
  释放 delayed push

EndTick:
  不提交 rotate 成员坐标变化
  body members 保持原权威坐标
  解锁 body members
```

碰撞发生后，回弹速度必须和配置计算出的 rotate 速度一致。

如果第 `k` 个 tick 弧段发生碰撞：

```text
OutTicks = k
ReturnTicks = k
EffectiveCostTicks = OutTicks + ReturnTicks
ContactTick = StartTick + OutTicks
EndTick = StartTick + EffectiveCostTicks
```

回弹时间不是额外配置，而是由已经旋出的 tick 数算出。旋出用了多少 tick，返回就用多少 tick，保证回弹速度和配置推导出的 rotate 速度一致。

例如：

```text
CostTicks = 6
anglePerTick = 15 度
第 2 个 tick 弧段撞到 blocker

StartTick -> ContactTick: 2 tick, 旋出 30 度
ContactTick -> EndTick: 2 tick, 从 30 度回弹
EffectiveCostTicks = 4
```

## Contact Tick 内的 Push

`ContactTick` 内扫到的所有 blocker 都会产生 impact / delayed push。

规则：

- 同一 tick 弧段内不比较 7 度、19 度这类内部先后。
- 同一 tick 弧段内扫到的 blocker 视为同一逻辑时刻被撞到。
- 多个成员扫到不同 blocker 时，全部收集。
- 同一个 blocker 或同一个 handoff subject 需要去重。
- 每个 impact 必须保留来源上下文。

impact 上下文至少包括：

```text
ImpactMemberId
ImpactFromCoord
ImpactToCoord
BlockerEntityId
PivotEntityId
PivotCoord
RotateDirection
PushDirection
OwnerActionId
```

delayed push 的方向来自具体撞击成员在该弧段的切向位移方向，不是原始 push 方向，也不是 rotate direction 的枚举名。

## Handoff Subject

rotate 撞到 blocker 后派生 delayed push 时：

- 当前 spec 必须启用 handoff。
- 如果 blocker 属于外部 connected body，且 subject policy 是 `ConnectedBodyIfAny`，则整个外部 connected body 作为 handoff subject。
- rotate 只负责把 contact tick 内的撞击事实转换成 delayed push 输出，然后自身按 bounce 返回。
- delayed push 输出进入后续正式 push 管线后，能不能被目标消费、是否失败、是否继续 handoff，由 push 管线自己裁决。
- rotate 不因为后续 push 消费失败而回滚，也不等待后续 push 的结果。
- rotate impact push 必须进入正式 `DeferredAction` / push contribution / dedupe / merge 管线，不能降级为动画 metadata。

## Push Spec 语义

rotate 撞击产生的 push 不应该分裂出新的业务 spec。

push 就是 push。rotate 只是这次 push 的来源上下文，不改变 push 的本质。

规则：

- 不新增 `rotate_impact_push` 这类独立业务 spec。
- delayed push 使用原有 push / handoff 语义进入队列。
- rotate impact 只通过 `PushOriginContext.RotatePivotImpact` 标记来源。
- 后续合并、抵消、阻挡、继续 handoff 都按普通 push 管线处理。
- 表现层如果需要区分“这是 rotate 撞出来的 push”，读取 origin context 或 presentation fact，不靠 action spec 分裂。

这样所有 push 本质同源，业务不会因为来源不同而分裂成多套 push 规则。

## Source Action Result 语义

rotate 的 source action result 不应该被简单理解成传统移动的成功或失败。

原因是 rotate 有三类结果：

- 成功旋转并提交最终姿态。
- 碰撞后产生 impact / delayed push，然后 rotate 自身回弹。
- 没有进入 rotate 或输入无效。

其中碰撞回弹分支不是“移动成功”，也不是普通“失败”。它表示本次 push 被 rotate-pivot 响应消费，并产生了权威 contact 输出。

当前推荐口径：

- rotate success：source action result 可视为成功，最终坐标在 end tick 提交。
- rotate bounce：source action result 应表达为“已处理并产生 deferred output”，reason 使用 `rotate-pivot-deferred-output` 或等价原因。
- rotate invalid：source action result 才按失败处理，例如多 pivot、torque 抵消等。

后续如果现有 `MoveResult.Success` 布尔值不足以表达这个中间语义，应增加更明确的 action outcome，而不是把 bounce 强行塞进成功或失败之一。

## TimedActionUnit 边界

rotate 是 guaranteed physical timed action。

当前默认策略：

```text
CompletionMode = CompleteAtEndTick
IncomingPolicy = RejectIncoming
```

active rotate 期间，如果 incoming action 命中 rotate member 或其 subject：

- 请求失败。
- reason 为 `timed-subject-in-flight`。
- 不排队。
- 不打断当前 rotate。

`DeferIncomingUntilEnd` 可以作为未来配置能力，但它必须在 end tick 后重新进入 targeting / arbitration，不能复用旧世界事实。

## PresentationFact 语义

服务端必须下发足够事实，让客户端不用猜。

`RotatePivotGroup` 需要表达：

```text
FactType = RotatePivotGroup
ResultKind = Success | Bounce
StartTick
ContactTick
EndTick
EffectiveCostTicks
PivotEntityId
PivotCoord
RotateDirection
Members[]
Impacts[]
```

`RotatePivotImpact` 需要表达：

```text
FactType = RotatePivotImpact
ResultKind = Impact
ServerTick = ContactTick
BlockerEntityId
ImpactMemberId
ImpactFromCoord
ImpactToCoord
PushDirection
PivotEntityId
PivotCoord
RotateDirection
```

客户端不从 before / after snapshot 推断 rotate 成员、碰撞对象、contact tick、bounce 时长或 push 释放时机。

## 客户端播放语义

客户端表现层只消费服务端 facts 和 snapshot。

成功 rotate：

```text
StartTick -> EndTick:
  group 绕 pivot 播放 0 -> 90 度

EndTick:
  对齐 after snapshot
```

bounce rotate：

```text
StartTick -> ContactTick:
  group 绕 pivot 播放 0 -> contact angle

ContactTick:
  播放 blocker impact feedback

ContactTick -> EndTick:
  group 从 contact angle 回弹到 0

EndTick:
  对齐 after snapshot
```

客户端可以决定 easing、视觉缩放、闪烁、线条跟随，但不能改变 phase 边界。

connected body 播放必须是 group 级别：

- 成员临时归属同一个 group track。
- 端口线跟随当前 group pose。
- 普通 entity animation 不能覆盖 group 成员。
- 播放完成后释放 group ownership，并对齐权威 snapshot。

## 与当前代码的差异

当前代码已经具备较好的分层：

- `TimedActionUnit` 已抽出跨 tick 生命周期。
- `RotatePivotResponseProcessor` 已集中 rotate resolution。
- `StateDrivenRuleExecutionSystem` 已负责 timed release、incoming reject 和主管线接入。
- 客户端已有 RotatePivot group 播放雏形。

但 rotate sweep 语义需要升级：

当前代码倾向于固定 90 步细采样，再用 progress 计算 contact tick。

目标语义应改为：

```text
CostTicks 决定 tick 弧段
每个 tick 弧段做 connected body 扇形格子 sweep
第一个有 contact 的 tick 释放该 tick 内所有 contacts
bounce 回弹时间等于已旋出的 tick 数
```

这不是表现优化，而是逻辑语义修正。

也就是说，之前准备的算法是“tick 弧段 shape sweep”这一层语义；现在需要把它落成更具体的“每个成员绕 pivot 的扇形范围检测”。固定 90 细采样只是旧代码倾向，不应该作为后续重构算法。

## 不该采用的方案

不要回到以下做法：

- 用 Unity 物理检测决定 rotate 逻辑。
- 用客户端动画进度推断 contact。
- 用固定 90 细采样作为业务时间源。
- 只检查最终目标格，不检查旋转弧段。
- 把 connected body 当单个中心点扫掠。
- tick 内继续按角度排序决定哪个 blocker 先撞。
- rotate 撞击只发动画，不生成正式 delayed push。
- 旋转期间允许普通外部 action 默默覆盖 member 坐标。

## 自动测试要求

只使用 Unity TestFramework。

需要覆盖：

- 无 pivot 时回落普通 push。
- 唯一 pivot 时进入 rotate。
- 多 pivot 时 rotate 无效。
- push 穿过 pivot 时回落普通 push。
- 同 tick 同 body 多个 push 按 torque contribution 合成。
- 正反 torque 抵消时 rotate 无效。
- `CostTicks = N` 时按 N 个 tick 弧段 sweep。
- 每个 tick 弧段按成员扇形范围检测 swept cells，不依赖固定 90 次采样。
- 扇形检测覆盖弧段中途扫过格，即使最终目标格没有 blocker 也能 contact。
- 第一个 contact tick 内所有 blocker 都被收集。
- 后续 tick 弧段的 blocker 不产生 impact。
- tick 弧段末端格属于本 tick。
- rotating body 自己当前占用格不阻挡自身。
- rotate success 在 start tick 不提交坐标，在 end tick 提交坐标和方向。
- rotate bounce 在 contact tick 释放 impact / delayed push，在 end tick 解锁且不提交坐标变化。
- bounce 回弹 tick 数等于已旋出 tick 数。
- rotate impact push 进入正式 deferred action，并保留 origin context。
- active rotate subject 期间 incoming action 默认 reject。
- 客户端 `RotatePivotGroup` 使用服务端 tick phase 播放。
- bounce 播放结束后回到 after snapshot。

## 手动端到端验证

用户需要在 Unity Play Mode 和服务端权威环境验证：

1. 启动 Fantasy 服务端。
2. 启动 Unity Play Mode 客户端。
3. 如支持双客户端，启动两个客户端观察同一 WorldDelta 序列。
4. 摆放唯一 pivot 的 connected body，push 后确认整体绕 pivot 旋转 90 度。
5. 调整 `CostTicks`，确认旋转速度和逻辑 contact tick 同步变化。
6. 在第一个 tick 弧段内摆放多个 blocker，确认同 tick 产生多个 impact / delayed push。
7. 在后续 tick 弧段摆 blocker，确认如果前面已 contact，后续 blocker 不产生 impact。
8. 验证 bounce 回弹速度和旋出速度一致。
9. 验证 rotate 期间推成员会被服务端拒绝，客户端不本地裁决。
10. 验证双客户端最终坐标、方向、impact 表现一致。
