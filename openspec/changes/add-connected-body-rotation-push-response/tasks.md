## 1. 规格与配置边界
- [x] 1.1 确认 `RotatePivotComponent` 的运行时字段为空标记或最小数据结构。
- [x] 1.2 在 Luban component/config 导入边界加入旋转点组件映射。
- [x] 1.3 确认新增组件不需要修改 `.meta` 文件。
- [x] 1.4 在 RuntimeEffect payload / EffectSpec 配置中加入 rotate-pivot 结果。
- [x] 1.5 在 ComponentStateResolver 结果合成中支持静态 pivot 与 runtime pivot 来源共存。
- [x] 1.6 覆盖 runtime pivot 过期或移除时保留静态 pivot 和其他 runtime pivot 来源。

## 2. Torque 仲裁
- [x] 2.1 增加 push contribution 到 torque contribution 的转换，输入包含 pivot、接触成员、push direction、contribution count、causality samples。
- [x] 2.2 覆盖 CW/CCW/zero torque 判定。
- [x] 2.3 覆盖多 push 同向 torque 只产生一次 90 度旋转。
- [x] 2.4 覆盖 CW/CCW 按贡献量抵消，抵消为 0 时不旋转。
- [x] 2.5 保留现有 push vector metadata，不破坏普通平移 push 合成。
- [x] 2.6 覆盖 push 向量经过 pivot 时不触发旋转，回退为普通 connected body 平移。

## 3. 旋转目标规划
- [x] 3.1 connected body 内 0 个旋转点时保持普通 push 平移语义或按配置禁用旋转响应。
- [x] 3.2 connected body 内恰好 1 个旋转点时进入旋转响应。
- [x] 3.3 connected body 内多个旋转点时本次 push 无效，不派生 push，不移动。
- [x] 3.4 按 pivot 计算所有成员 90 度旋转后的目标格。
- [x] 3.5 允许同一 rotating body 当前占格作为可释放空间。
- [x] 3.6 旋转后目标格重复时取消本次旋转。
- [x] 3.7 旋转成功时同步旋转成员的 `DirectionComponent`，而不是只平移坐标。
- [x] 3.8 旋转目标规划检查成员从原格到目标格之间的扫略格，扫略路径上的外部 blocker 也会阻挡旋转。

## 4. 阻挡与延迟 push
- [x] 4.1 旋转目标无外部阻挡时提交全体旋转成员位置。
- [x] 4.2 旋转目标被外部 blocker 阻挡时收集全部阻挡接触。
- [x] 4.3 外部 blocker 属于 connected body 时以整个外部 connected body 为 handoff subject。
- [x] 4.4 为全部可推动外部 blocker subject 生成 delayed push。
- [x] 4.5 旋转遇阻并生成 delayed push 时，本次旋转自身取消且不提交自身位置。
- [x] 4.6 任意 blocker 不可被 push 时取消本次旋转且不提交 delayed push。
- [x] 4.7 delayed push 方向按具体撞击子成员的旋转切向位移计算。
- [x] 4.8 delayed push 记录 impact member、impact from/to、blocker、pivot、rotate direction、source action 和 causality samples。
- [x] 4.9 多个撞击合并为同一 downstream subject 时保留每个 impact contribution context。

## 5. 管线接入
- [x] 5.1 通过注册策略、响应模块或等价显式模块接入，不新增 action-name 分支。
- [x] 5.2 确保 rotate-pivot body 消费 push，不再叠加普通平移。
- [x] 5.3 确保普通 connected body push 平移、push chain、deferred output 现有语义不变。
- [x] 5.4 确保服务端权威 tick 和 Unity 客户端镜像只通过 WorldDelta 观察结果。
- [x] 5.5 为 rotate-pivot 表现新增稳定 styleId / motion id 配置，不新增 gameplay-extension enum 成员或 action-name 表现分支。
- [x] 5.6 确保同一 tick 同一 resolved subject 只产生一个最终行为结果：旋转、延迟输出、普通平移或无效之一。
- [x] 5.7 让 rotate-pivot impact push 进入正式 WorldAction / ActionRequest / push contribution 数据，而不是只写动画 metadata。
- [x] 5.8 确保普通 push 与 rotate-pivot impact push 使用同一 dedupe / equivalence / contribution merge 语义，并保留各自 origin context。

## 6. 自动测试
- [x] 6.1 Unity TestFramework EditMode：唯一 pivot body 被单个 push 驱动旋转 90 度。
- [x] 6.2 Unity TestFramework EditMode：多个同向 torque contribution 合成一次 90 度旋转。
- [x] 6.3 Unity TestFramework EditMode：反向 torque contribution 按数量抵消。
- [x] 6.4 Unity TestFramework EditMode：push 向量经过 pivot 时不触发旋转，回退为普通 connected body 平移。
- [x] 6.5 Unity TestFramework EditMode：多个 pivot 无效且不移动、不派生 push。
- [x] 6.6 Unity TestFramework EditMode：内部当前占格不阻挡，目标重复取消。
- [x] 6.7 Unity TestFramework EditMode：旋转遇多个外部 blocker 生成全部 delayed push 并取消自身。
- [x] 6.8 Unity TestFramework EditMode：外部 blocker connected body 作为整体 push subject。
- [x] 6.9 Unity TestFramework EditMode：普通 push vector 平移测试保持通过。
- [x] 6.10 Unity TestFramework EditMode：旋转撞击 push 方向来自 impact member 切向位移。
- [x] 6.11 Unity TestFramework EditMode：同一 subject 多个旋转撞击 contribution 合并后保留每个 impact context。
- [x] 6.12 Unity TestFramework EditMode：RuntimeEffect 添加 rotate pivot 后 connected body 进入旋转响应。
- [x] 6.13 Unity TestFramework EditMode：RuntimeEffect 过期或移除后静态 pivot 保留，runtime-only pivot 消失。
- [x] 6.14 Unity TestFramework EditMode：旋转成功后成员方向也按 90 度更新。
- [x] 6.15 Unity TestFramework EditMode：旋转扫略路径被 blocker 阻挡时，即使最终目标格为空也生成 delayed push 并取消自身。

## 7. 构建与服务端验证
- [x] 7.1 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 7.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 7.3 运行相关 Unity EditMode 测试。

## 8. 手动端到端验证
- [ ] 8.1 用户启动 Fantasy 服务端和 Unity Play Mode。
- [ ] 8.2 用户启动两个客户端观察同一个 WorldDelta 序列。
- [ ] 8.3 用户摆放带唯一 pivot 的连接体，确认 push 后旋转 90 度且两个客户端一致。
- [ ] 8.4 用户摆放多个 pivot 的连接体，确认 push 无效且两个客户端一致。
- [ ] 8.5 用户摆放旋转阻挡链，确认旋转体不移动，全部 blocker 收到 delayed push，两个客户端一致。
- [ ] 8.6 用户通过正式 buff/effect 给连接体子成员添加 pivot，确认生效、过期或移除后两个客户端一致。

## 9. 旋转动画层与协议
- [x] 9.1 扩展 WorldDelta 旋转动画 metadata，保留旧字段并追加 pivot、from、to、impact、rotate direction、bounce 字段。
- [x] 9.2 通过 Fantasy 协议导出工具生成服务端和 Unity 客户端协议代码。
- [x] 9.3 在 rotate-pivot 成功提交时，为每个 connected body 成员写入 RotatePivot 动画 metadata，pivot 成员也写入原地旋转表现。
- [x] 9.4 在 rotate-pivot 遇阻取消时，为每个 connected body 成员写入 RotatePivotBounce 动画 metadata，并为被撞 blocker 写入切线方向 impact/push 反馈 metadata。
- [x] 9.5 为 rotate-pivot 动画补充可配置 styleId：`rotate_pivot`、`rotate_pivot_bounce`、`rotate_pivot_impact`。
- [x] 9.6 Unity 客户端网络转换保留新增旋转字段，并映射 RotatePivot / RotatePivotBounce motion kind。
- [x] 9.7 Unity AnimationLayer 生成包含 pivot/from/to/impact 的旋转动画事件，支持成功旋转和 bounce 场景。
- [x] 9.8 ClientWorldVisuals 按 pivot 做整体刚体式圆弧插值，成员相对布局保持静止；bounce 动画沿圆弧撞击后回原位。
- [x] 9.9 Unity TestFramework EditMode：成功旋转 metadata 能生成每成员旋转事件，包含 pivot 成员原地旋转事件。
- [x] 9.10 Unity TestFramework EditMode：blocked metadata 能生成旋转体 bounce 事件和 blocker impact 反馈事件。
- [x] 9.11 Unity TestFramework EditMode：旋转播放中点落在圆弧路径而不是线性路径，bounce 播放结束回到快照原位。
- [x] 9.12 服务端 authoritative verification：成功旋转和 blocked 旋转的 metadata 字段完整且两端同步序列确定。
- [ ] 9.13 手动端到端验证：双客户端看到连接体整体绕 pivot 旋转；被阻挡时连接体弹回、blocker 有同 tick 冲击反馈，后续 delayed push 继续使用普通 push 动画。
