# 当前 Push 业务逻辑归纳

本文归纳当前代码中 push 相关业务逻辑。范围以 `Shared/DG.GameCore/ActionRuntime`、`Shared/DG.GameCore/Domain/Components`、Luban 生成配置、Unity EditMode 测试为准。

## 核心结论

当前 push 不是一个独立系统，也不是 commit 阶段的递归位移逻辑。它是 action pipeline 里的移动类行为：

1. `WorldAction` 进入 `StateDrivenRuleExecutionSystem.Tick`
2. 转成 `ActionRequest`
3. 由 action strategy 路由到 move request
4. 旋转枢轴响应先处理特殊 connected body 旋转
5. 同 tick push 请求先做向量合成
6. `ActionArbiter` 做目标、主体、能力、tag、阻挡和 handoff 裁决
7. `RulePlanner` 把 accepted action 转成 `MovePlan`
8. `ConflictResolver` / `CommitResolver` 提交最终世界状态
9. 被阻挡产生的后续 push 通过 `DeferredAction` 回到队列，在之后 tick 再处理

也就是说，push 的业务真相在 action 配置、主体解析、阻挡策略、deferred output 和最终 commit 的组合里。

## 关键组件

`PushableComponent` 表示实体可以作为 push 入口。一个实体没有该组件，就不能被阻挡分支当成可推动目标。

`MovementPermissionComponent.CanBePushed` 会进一步限制 push 入口。实体有 `PushableComponent` 但 `CanBePushed == false` 时，仍然不能被 push。

`PushOnEnterComponent` 表示地格上的进入或停留输出能力。它只保存输出 action spec 和 cost：

- `OutputSpecId`
- `OutputCostTicks`

地格本身不写死是传送带、风场还是其他机关。业务差异来自 Luban 配置。

`ActionSpec` 决定 push 的来源、优先级、目标选择、主体策略、阻挡结果和 handoff 规则。当前主要 push 相关 spec：

- `player_move`：玩家移动，遇到可推动阻挡时 handoff 到 `player_push`
- `player_push`：玩家推动后续目标
- `mechanism_push`：机关推动，默认 cost 是 3 tick
- `configured_wind_push`：配置化风场推动，默认 cost 是 1 tick
- `connected_body_move`：连接体移动类 action

`BlockedResultPolicy` 决定遇阻后怎么处理：

- `push_or_block`：可推动则派生 action，否则阻挡
- `immune_push_or_block`：先判断 `ImmuneMechanismPush`，免疫则拒绝；否则可推动则派生 action；再否则阻挡
- `bounce_or_block`：可反弹来源先 bounce，否则阻挡
- `reject_only`：直接拒绝

## PushOnEnter 业务

进入推动地格由 `ExplicitOutputPolicies.EnqueuePushOnEnterActions` 产生 action。

处理规则：

1. 查询所有带 `PositionComponent`、`DirectionComponent`、`PushOnEnterComponent` 的实体
2. 对同格上其他 positioned entity 生成配置化 move action
3. action spec 使用 `PushOnEnterComponent.OutputSpecId`
4. action cost 使用 `PushOnEnterComponent.OutputCostTicks`
5. 同一个 tick 内同一个目标只会由 PushOnEnter 直接入队一次

当前配置里：

- `config_id = 2001` 输出 `mechanism_push`，cost 1
- `config_id = 2002` 输出 `configured_wind_push`，cost 2

传送带和风场只是配置与 archetype 的表现差异，规则层读的是 `PushOnEnterComponent`。

## 普通 push handoff

当一个 move-like action 的移动 claim 目标格被外部 blocking entity 占用时，`BodyCapabilityResolver.FindExternalPushContacts` 会收集阻挡接触。

`ActionBlockedOutcomeExecutor` 根据当前 action 的 `BlockedResultPolicy` 选择结果分支。若命中 `DeriveAction`：

1. 阻挡实体必须通过 `CanPushEntry`
2. 当前 action 必须有方向
3. 当前 spec 必须开启 handoff
4. 根据分支的 `ResultSpecId` 和 `SubjectKind` 生成 `DeferredAction`
5. 当前 source action 记录成功结果，原因是 `bounded/deferred-output`
6. 后续推动不会让 source action 等待下游成功或失败

这里的关键边界是：push handoff 不创建 pending child，不让父 action 阻塞等待子 action，也不在同一个 commit 里把整条链一次性推完。

## DeferredAction 与队列合并

`DeferredAction` 是 push 向后传播的主要载体。

它携带：

- 下游 action spec
- 被推动入口 entity
- connected body subject ids
- direction
- created tick / ready tick / cost
- causality id
- dedupe key
- push origin context

`WorldActionQueue.EnqueueDeferred` 会按等价键合并相同 ready tick、相同 spec、相同 direction、相同 subject 的 deferred output。合并后不会多生成一批 action，而是增加贡献数量和因果样本。

相反方向、不同 subject、不同 ready tick 或不同 spec 不会被合并。

## 同 tick push 向量合成

在 action arbiter 前，`PushVectorArbiter` 会对同一 ready tick、同一 subject 的 push contribution 做合成。

合成范围只覆盖满足以下条件的请求：

- targeting selector 是 `direction_cell`
- blocked result policy 里存在 `DeriveAction` 分支
- request 有明确 direction

合成规则：

- 相反方向按贡献数抵消
- 净向量为 0 时，请求失败，原因是 `push-vector-cancelled`
- 同方向多个贡献不会增加移动距离，只保留一个代表请求
- X/Y 双轴同时存在时，路径按 X 再 Y 拆成方向路径，目前只执行第一步，并记录 `push-vector-path-deferred`
- 合成后的请求仍要进入后续碰撞与 commit 校验，不会绕过阻挡规则

connected body subject 会用成员 id 排序后的组合 key 合成，避免同一个连接体被不同入口当成不同 subject。

## Connected Body 规则

当前 push 支持 connected body，但入口和主体是分开的。

入口规则：

- `PushableComponent` 只对被接触的成员生效
- connected body 中 A 可推动，不代表 B 也自动可推动
- push 必须打到合法入口成员，才会把整个 body 作为 subject

主体规则：

- `ActionSubjectKind.ConnectedBodyIfAny` 会把 port-connected group 解析成同一个 `BehaviorBody`
- 被接受后，整个 body 生成一组 `BodyMove` claim
- 任意成员目标格被外部 blocking 挡住，整个 body 的当前移动都不能部分提交
- 下游 handoff 到 connected body 时，`DeferredAction.SubjectEntityIds` 保存完整成员集合

因此 connected body push 的结果是整体平移、整体失败、或整体向下游派生 deferred output，不存在只移动一部分成员的普通 push commit。

## 多接触 push

connected body 移动时可能多个成员同时撞到外部目标。当前逻辑会收集 distinct external contacts。

如果全部 contact 都能作为 push entry，则会生成多个 deferred output。source action 自己不等待这些 deferred output 完成。

如果任意 contact 不满足 push entry 条件，当前 blocked step 会整体拒绝，不做部分 handoff。

## 旋转枢轴与 push

旋转枢轴是 push 前置处理的一种特殊响应，由 `RotatePivotResponseProcessor` 在普通 push 向量合成前处理。

当前语义：

- 有唯一 pivot 的 connected body 被合适方向推动时，可以生成 timed rotation
- 旋转 sweep 碰到 pushable 外部目标时，会在接触 tick 产生 deferred push
- 旋转中的 subject 会拒绝新的 incoming action，原因是 `timed-subject-in-flight`
- 旋转 impact 的上下文通过 `PushOriginContext` 进入 deferred output，合并时保留最多 8 个上下文
- 外部 connected body blocker 会成为完整 handoff subject

如果没有合法 pivot，或 push 方向穿过 pivot，逻辑会回退到普通 connected body 平移或失败。

## 免疫与能力标签

`mechanism_push` 和 `configured_wind_push` 当前配置了 `ImmuneMechanismPush` 相关限制。

规则效果：

- 阻挡目标带 `ImmuneMechanismPush` 时，`immune_push_or_block` 优先拒绝
- source 或 subject 被 tag gate 拦截时，不进入后续 push 传播
- 规则层不通过 action 名称写死免疫、风场、传送带差异，差异来自配置和 tag/component 事实

## 失败边界

push 失败主要来自这些条件：

- 缺少实体或 position
- 请求没有目标或方向
- source body 不能移动
- tag gate 不通过
- 阻挡目标不是 push entry
- 阻挡目标是玩家占位并按策略拒绝
- connected body 目标格冲突
- 多个 action 竞争同一 target cell
- 同 subject 同 tick 相反 push 抵消
- timed subject 正在执行旋转或 bounce

失败只影响对应 action。已经完成的 source action 不会因为之后的 deferred push 失败而回滚。

## 当前验证覆盖

Unity EditMode 测试主要覆盖在这些文件：

- `Client/DG_Client/Assets/Tests/Editor/ClientWorld/Rules/PushOnEnterTileTests.cs`
- `Client/DG_Client/Assets/Tests/Editor/ClientWorld/Rules/DataDrivenRuntimeActionTests.cs`
- `Client/DG_Client/Assets/Tests/Editor/ClientWorld/Rules/RotatePivotPushResponseTests.cs`

重点测试点：

- PushOnEnter 从 Luban 配置读取 output spec 和 cost
- 传送带不阻挡玩家进入
- mechanism push 成功移动、目标阻挡时保持原位
- 单 tick 同目标只直接推动一次
- `mechanism_push` 默认 cost 为 3 tick
- handoff 创建 `DeferredAction`
- 普通 push 不创建 pending child
- deferred output 到 ready tick 后重新入队
- source action 不等待下游 deferred result
- connected body 整体移动、整体失败、合法入口推动整体
- 非 pushable linked member 不能借用其他成员的 pushable
- 多接触 push 生成多个 deferred output
- 同 tick 相反 push 向量抵消
- 同方向合成后仍做碰撞校验
- 旋转枢轴 sweep contact 产生 deferred push
- timed subject 执行中拒绝 incoming action
- 配置变化可以改变 derive/block 行为，不需要改核心规则模块

## 手动端到端验证

建议用 Unity Play Mode 或现有调试布局验证这些场景：

1. 玩家推单个 pushable 方块，方块在下一 ready tick 移动，玩家 action 不等待方块结果
2. 玩家推两个连续 pushable，推动按 deferred output 分 tick 传播，尾端先移动
3. 传送带上放玩家，tick 后按传送带方向输出 `mechanism_push`
4. 风场上放玩家，确认按 `configured_wind_push` 的 cost 延迟后移动
5. 给目标加 `ImmuneMechanismPush`，确认机关 push 被拒绝
6. 推 connected body 的 pushable 成员，确认整个 body 一起移动
7. 推 connected body 的非 pushable 成员，确认不会推动整个 body
8. 制造同 tick 相反方向 push，确认目标不动并出现 `push-vector-cancelled`
9. 旋转枢轴 connected body 撞到 pushable，确认 contact tick 生成 deferred push 和对应表现

## 不该回退的旧做法

当前逻辑不应该回到这些方式：

- 在 commit 阶段递归推完整条链
- 用 action 名称分支写死传送带、风场、玩家推箱
- 让父 action pending 等待子 push 完成
- 让 connected body 的某个成员 pushable 自动扩散到所有成员
- 在客户端镜像代码里本地裁决 push 成功、失败或传播
- 跳过 `ActionSpec -> ActionRequest -> Arbiter -> Plan -> Commit` 管线直接改坐标
