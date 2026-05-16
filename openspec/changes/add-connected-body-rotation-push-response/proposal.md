# Change: 新增连接体受 push 后的旋转响应

## Why
当前 connected body 被通用 push 命中后只能按平移语义处理。需要支持一类带唯一旋转点的连接体：它被 push 命中时把推力转换成绕旋转点的 90 度旋转；旋转被外部阻挡时不移动自身，而是产生延迟的通用 push 输出。

## What Changes
- 新增 `RotatePivotComponent` 或等价运行时组件语义，用于标记 connected body 内唯一旋转点。
- 新增受 push 驱动的旋转响应策略：push contribution 先仲裁为 torque，再决定顺时针或逆时针 90 度旋转。
- 明确同一 connected body 内 0 个或多个旋转点时的无效行为。
- 明确旋转体消费 push，不叠加普通平移。
- 明确旋转目标内部占格可释放，目标重复或外部不可推动阻挡会取消旋转。
- 明确旋转遇到外部 pushable blocker 时，按具体撞击子成员的旋转切向方向为全部阻挡 subject 生成带完整 push 上下文的延迟 push，并取消本次旋转。
- 明确旋转撞击产生的 push 与普通 push 使用同一 contribution / causality / dedupe / merge 管线，只是来源上下文为 rotate-pivot impact。
- 明确 `RotatePivotComponent` 既可来自静态实体配置，也可由正式 RuntimeEffect / buff 配置在运行时添加、刷新、过期或移除。
- 新增 rotate-pivot 专用表现元数据：成功旋转时服务端为每个 connected body 成员输出带 pivot、from/to、旋转方向的动画上下文；遇阻时输出 bounce 上下文，表达成员沿圆弧撞击后回到原位。
- 明确 pivot 成员自身也参与旋转表现：它的坐标保持不变，但会随 connected body 的整体旋转表现同步更新方向/端口视觉。
- 明确 connected body 旋转动画按整体刚体表现播放，成员之间视觉相对位置保持静止，不做彼此独立的线性位移。
- 明确被撞 blocker 在同 tick 收到切向冲击反馈动画；若后续 delayed push 成功移动，则继续复用普通 push 动画。
- 扩展 WorldDelta / Fantasy 网络协议动画元数据字段，并通过 Fantasy 协议导出工具生成客户端和服务端协议代码，不手工维护生成物。
- 新增可配置动画样式键：`rotate_pivot`、`rotate_pivot_bounce`、`rotate_pivot_impact`；具体曲线策略先由客户端对应动画策略实现，样式配置负责 duration、easing、flash、scale 等可调参数。
- 新增 Unity TestFramework EditMode、Shared build、服务端验证和手动端到端验证要求。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `claim-driven-action-arbitration`, `authoritative-move-runner`, `runtime-component-results`, `client-world-runner`
- Affected code: Shared GameCore connected body resolution、push vector/torque 仲裁、ActionStrategy/RulePlanner/Commit/DeferredAction 路径、push origin context、RuntimeEffect/ComponentStateResolver、snapshot/WorldDelta 动画元数据、Fantasy 网络协议 proto 与生成代码、Server authoritative world sync、Luban component/effect/action/animation style provider、Unity ClientAnimationLayer、ClientWorldVisuals、Unity EditMode 测试、Server authoritative verification
- 不允许修改 `.meta` 文件；Unity 资源刷新由 Unity 自己生成。
