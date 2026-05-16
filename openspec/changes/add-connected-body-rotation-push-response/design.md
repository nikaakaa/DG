## Context
当前 push 管线已经具备 same tick push contribution 合成、connected body subject resolution、blocked handoff deferred output、WorldDelta metadata-only feedback 等能力。旋转响应不是玩家独立输入，也不是一个新 movement enum，而是通用 push 命中带唯一旋转点 connected body 后的 subject response。

## Goals
- 通用 push 可以驱动带唯一旋转点的 connected body 旋转 90 度。
- 多个 push contribution 对同一个旋转 body 按 torque 方向和贡献量仲裁。
- 旋转成功时只提交旋转成员位置，不再执行普通平移。
- 旋转遇到外部阻挡时按具体撞击子成员的旋转切向方向生成全部延迟 push，并取消本次旋转。
- 旋转撞击产生的 push 携带与普通 push 等价的正式上下文，进入 contribution、dedupe、merge、diagnostics 和 WorldDelta metadata 边界。
- 外部 connected body blocker 作为整体 subject 被 push。
- `RotatePivotComponent` 既支持静态实体配置，也支持正式 RuntimeEffect / buff 配置在运行时添加、刷新、过期和移除。
- 行为通过注册模块和配置选择进入管线，不增加 gameplay-extension enum，不增加 action-name 分支。
- 自动测试覆盖核心规则，端到端同步由用户在 Play Mode 手动验证。
- rotate-pivot 成功和遇阻时都提供服务端权威表现元数据，客户端不从快照差分猜测隐藏旋转上下文。
- Unity 客户端把 connected body 作为整体刚体播放旋转表现：成员相对位置保持静止，pivot 自身也参与方向/端口旋转表现。
- rotate-pivot 动画样式可配置；具体轨迹曲线先由每种客户端动画策略实现，不进入服务端规则裁决。
- blocked 场景同时表现旋转体 bounce 和 blocker impact：旋转体沿圆弧撞击后回原位，blocker 同 tick 获得切向冲击反馈，后续 delayed push 成功时继续走普通 push 动画。

## Non-Goals
- 不支持玩家直接输入 rotate action。
- 不支持任意角度、45 度、连续角速度或浮点旋转。
- 不引入长期存在的 MomentumComponent。
- 不把旋转点做成外部锚点。
- 不为 pivot 单独引入绕过 RuntimeEffect / ComponentStateResolver 的生命周期系统。
- 不在一次 tick 内递归求解整条 push 链。

## Decisions
- Decision: 旋转点来自 connected body 成员上的组件，且每个 connected body 必须恰好一个旋转点才启用旋转响应。
- Decision: push 先保留现有 contribution 记录，再在旋转响应层按接触点相对 pivot 的 radius 和 push direction 做叉积，得到 CW/CCW/zero torque。
- Decision: CW 和 CCW 贡献按 `DeferredContributionCount` 或等价 contribution count 抵消；抵消后只要剩余一侧，当前 tick 最多旋转 90 度一次。
- Decision: rotate-pivot body 在一个 tick 内只能消费一个最终行为；一旦 push 进入旋转响应，就只能产生旋转、延迟 push 输出或无效结果，不再回退普通平移。
- Decision: zero torque contribution 或抵消后 zero torque 表示本次旋转和本次 push 都无效，但保留诊断 metadata。
- Decision: 同一 rotating body 的当前占格在旋转目标校验中视为可释放；旋转后目标格重复则无效。
- Decision: 旋转遇到外部 blocker 时，按全部 blocker subject 生成 deferred push；本次旋转 action 自身失败或 noop，不提交自身位置。
- Decision: 外部 blocker 如果属于 connected body，handoff subject 为该外部 connected body 整体。
- Decision: 旋转响应消费 push；成功旋转后不会继续普通平移。
- Decision: 旋转撞击输出的 deferred push 方向来自具体撞击子成员从旋转前坐标到旋转后目标格的切向位移，而不是原始玩家 push 方向或旋转方向枚举。
- Decision: 每个旋转撞击 push 必须记录 impact member、impact from/to、blocker、pivot、rotate direction、source action、causality samples 等来源上下文；合并后仍保留 contribution context 列表。
- Decision: 旋转撞击 push 与普通 push 使用同一正式 push contribution 管线，后续 torque 仲裁、dedupe、合并、诊断和表现元数据不得把它降级为仅动画 metadata。
- Decision: `RotatePivotComponent` 是最终组件结果，静态来源和 RuntimeEffect 来源合成后供规则层读取；运行时 buff 移除只移除自己的来源，不能清掉静态 pivot 或其他 runtime pivot。
- Decision: rotate-pivot 表现元数据由服务端写入 `WorldDelta.AnimationMetadata`，而不是由客户端根据前后快照推断；blocked 场景最终快照不移动，必须显式携带 pivot、from/to、impact、旋转方向和 bounce 标记。
- Decision: 表现 metadata 采用“每个成员一条事件”，不采用“一个 body 事件嵌套成员列表”；每条事件绑定 `EntityId`，共享同一 pivot 坐标、server tick 和 rotate direction，贴合现有 `ClientAnimationLayer` 按实体排队模型。
- Decision: pivot 成员也收到 rotate-pivot metadata。它的 from/to 坐标可以相同，但仍携带 rotate direction，用于端口线、方向朝向或局部视觉跟随整体旋转。
- Decision: 成功旋转使用 `RotatePivot` motion kind 和 `rotate_pivot` style key；遇阻回弹使用 `RotatePivotBounce` motion kind 和 `rotate_pivot_bounce` style key；blocker 同 tick 冲击反馈使用 `rotate_pivot_impact` style key，可复用普通 push impulse 语义或显式 impact motion。
- Decision: 客户端旋转播放使用表现层浮点 sin/cos 或等价曲线策略；该计算只影响视觉，不参与服务端碰撞、占用或确定性裁决。
- Decision: 连续 server tick 新动画到达时，客户端用新事件替换同实体 active animation，起点取当前视觉位置或策略约定起点，最终必须落回最新权威 snapshot。
- Decision: Fantasy 协议字段只追加不重排；生成代码必须通过 `Tools/ProtocolExportTool/Fantasy.ProtocolExportTool.dll export --silent` 或等价 Fantasy 导出流程生成，不手工维护生成物。

## Risks / Trade-offs
- Risk: 旋转响应与现有 push vector 合成层混在一起会让普通平移行为变复杂。
  Mitigation: 将 torque 仲裁做成独立模块，复用 contribution 数据，不改变普通 push vector path 语义。
- Risk: 旋转遇阻生成多个 deferred push 可能造成循环。
  Mitigation: 复用现有 deferred output dedupe、cycle guard、cost tick 和 push pending boundary。
- Risk: 现有表现同步仍有 `WorldDeltaMotionKind` / `ClientAnimationMotionKind` enum 遗留。
  Mitigation: 旋转表现新增稳定 `styleId` / motion id 配置，作为表现层数据边界；不因为 rotate-pivot 玩法新增 gameplay-extension enum 或 action-name 分支。
- Risk: 将旋转撞击 push 当成普通 deferred push 会丢失“哪个子成员撞了哪个 blocker”的因果，导致客户端表现方向和直觉不一致。
  Mitigation: 将 rotate-pivot impact 建模为正式 push origin context，随 deferred action 入队并在 request / contribution / metadata 边界保留。
- Risk: RuntimeEffect pivot 和静态 pivot 同时存在时，移除 buff 可能误删静态 pivot。
  Mitigation: 复用 ComponentStateResolver 来源合成模型，按来源移除 runtime contribution，并用测试覆盖静态来源保留。
- Risk: 每成员一条旋转 metadata 会让多成员 connected body 的消息数量增长。
  Mitigation: 旋转体规模当前由格子 connected body 和调试沙盒约束，metadata 是每 tick 一次的表现数据；避免 body 嵌套协议带来的复杂度优先。
- Risk: 客户端单实体播放会让 connected body 看起来散开。
  Mitigation: 每个成员事件使用相同 pivot、方向、duration 和策略曲线，并在视觉层按 pivot-relative arc 计算，保持相对布局像刚体一样旋转。
- Risk: blocked bounce 视觉短暂偏离权威快照，可能被误解为逻辑移动。
  Mitigation: bounce metadata 明确 `Bounce=true` 且 from/to 坐标不提交移动；动画结束必须回到 snapshot 原位。

## Migration Plan
1. 增加旋转点组件、静态配置导入和 RuntimeEffect payload / resolver 来源合成。
2. 增加 torque 仲裁和旋转目标规划。
3. 接入 push subject response 路径，确保普通 body 仍按平移。
4. 接入 rotate-pivot impact push context、deferred push 输出和 WorldDelta presentation metadata。
5. 扩展 Fantasy 协议动画 metadata，通过 Fantasy 工具生成服务端和客户端协议代码。
6. 接入 Unity AnimationLayer 与 ClientWorldVisuals 的 rotate-pivot / bounce / impact 表现。
7. 补 Unity EditMode、Shared build、Server verification 与手动验证说明。
