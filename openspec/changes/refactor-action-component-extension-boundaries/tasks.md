## 1. 动作策略扩展边界
- [x] 1.1 增加 `ActionStrategyId`，作为运行时策略选择键。
- [x] 1.2 调整 `ActionSpec` 读取 strategy id，并保留 `ActionPrimitive` 兼容映射。
- [x] 1.3 调整 Luban action policy 定义，新增 `strategy_id` authoring 字段。
- [x] 1.4 调整 Luban provider，将 authoring 策略名解析为运行时 strategy id。
- [x] 1.5 调整 `ActionStrategyRegistry`，按 strategy id 注册和查找策略。
- [x] 1.6 保留现有 `move`、`spawn`、`remove`、`runtime_effect` 策略语义。
- [x] 1.7 增加测试专用 action strategy，证明新增策略小类不需要修改 `StateDrivenRuleExecutionSystem`。
- [x] 1.8 增加 Unity TestFramework EditMode 测试，覆盖未知 strategy id 显式失败。

## 2. Commit handler 注册边界
- [x] 2.1 定义 commit proposal handler 接口，输入 world、proposal、commit 上下文，输出 `CommitProposalResult`。
- [x] 2.2 建立 commit handler registry，包含现有 move、direction、create、delete、auto tick、runtime effect、component result、tag、clear runtime sources。
- [x] 2.3 将 `CommitResolver` 的中心 `proposal.Kind` 分发替换为 registry 查找。
- [x] 2.4 保持 move 提交的占位、移动组和阻挡验证语义不变。
- [x] 2.5 增加测试专用 handler 路径，证明新增 handler 不需要修改 `CommitResolver` 主循环。
- [x] 2.6 增加 Unity TestFramework EditMode 测试，覆盖未知 commit handler 显式失败。
- [x] 2.7 增加现有移动、创建、删除、runtime effect commit 回归验证。

## 3. 静态 component applicator 扩展边界
- [x] 3.1 增加 `ComponentId`，作为配置和运行时组件能力键。
- [x] 3.2 调整实体 archetype，使组件列表读取 component id，并保留现有 `ComponentKind` 兼容导入。
- [x] 3.3 调整 Luban entity archetype 定义和兼容读取边界。
- [x] 3.4 定义 applicator 注册边界。
- [x] 3.5 将现有 Position、Direction、Collider、Blocking、Bouncable、AutoMove、PlayerControl、PushOnEnter、Pushable、PortConnector 迁移为 applicator 注册项。
- [x] 3.6 调整 `EntityBuilder`，只依赖 applicator registry。
- [x] 3.7 增加测试专用 component applicator，证明新增静态 component 只需小类和配置。
- [x] 3.8 增加 Unity TestFramework EditMode 测试，覆盖未知 component id 显式失败。

## 4. component 查询与规则条件边界
- [x] 4.1 建立 `ComponentFactQueryRegistry`，负责按 component id 判断实体是否拥有 final component。
- [x] 4.2 调整 target filter 的 component 条件，使用 component id 查询 registry。
- [x] 4.3 调整 blocked condition 的 component 条件，使用 component id 查询 registry。
- [x] 4.4 保持现有 component 条件语义不变。
- [x] 4.5 增加测试专用 component fact query，证明新增可查询 component 不需要修改 targeting 或 blocked resolver。

## 5. 运行时 component result resolver 扩展边界
- [x] 5.1 增加 `ComponentResultId`，替代中心 `ComponentResultKind` 分发作为扩展键。
- [x] 5.2 定义 component result resolver 接口，负责从静态和运行时 source 合成 final component。
- [x] 5.3 将 Blocking、AutoMove、Pushable、PortConnector、MovementPermission 迁移为 resolver 注册项。
- [x] 5.4 将 WorldTag source settlement 保持为独立 tag 解析边界。
- [x] 5.5 调整 runtime effect payload 到 component result source 的转换，按 result id 解析。
- [x] 5.6 调整 `ComponentStateResolver`，只负责收集 source、排序、调用 resolver、应用变化。
- [x] 5.7 增加测试专用 runtime component result，证明新增结果不需要修改 `ComponentStateResolver` 主流程。
- [x] 5.8 增加 Unity TestFramework EditMode 测试，覆盖 source remove 后 remaining source 仍保留 final result。

## 6. Snapshot / Delta component 同步扩展边界
- [x] 6.1 定义 snapshot component projector / applier 边界。
- [x] 6.2 迁移现有 Position、Direction、Collider、Blocking、Bouncable、AutoMove、PlayerControl、PushOnEnter、Pushable、PortConnector、MovementPermission 的 snapshot 语义。
- [x] 6.3 调整 `GameWorld.CreateSnapshot`，不再作为新增 component 同步字段的唯一修改点。
- [x] 6.4 调整 `GameWorld.ApplySnapshot`，不再作为新增 component mirror 应用的唯一修改点。
- [x] 6.5 保持现有 WorldDelta changed/removed/animation metadata 语义不变。
- [x] 6.6 增加测试专用 snapshot projector，证明新增同步 component 不需要修改 `GameWorld.CreateSnapshot` 主流程。

## 7. 兼容迁移与清理
- [x] 7.1 为旧 `ActionPrimitive`、`ComponentKind`、`ComponentResultKind` 提供迁移期解析。
- [x] 7.2 更新 fallback provider，仍只作为测试路径。
- [x] 7.3 更新 Luban 定义和生成代码兼容读取。
- [x] 7.4 更新相关说明，说明新增动作、静态 component、运行时 component result、同步 component 的步骤。
- [x] 7.5 检查中心分发残留，普通新增行为不通过 action 名字、entity 名字或 tag 组合分支。

## 8. 自动验证
- [x] 8.1 运行 `openspec validate refactor-action-component-extension-boundaries --strict --no-interactive`。
- [x] 8.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 8.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 8.4 运行 Unity TestFramework EditMode 测试，覆盖本变更新增扩展点。
- [x] 8.5 确认现有玩家移动、自动移动、机关推动、debug spawn/move/remove、runtime effect 测试通过。

## 9. 手动端到端验证
- [ ] 9.1 用户启动 Fantasy 服务端。
- [ ] 9.2 用户启动两个 Unity Play Mode 客户端并 JoinWorld。
- [ ] 9.3 用户验证已有玩家移动、自动移动、机关推动仍通过服务端 WorldDelta 同步。
- [ ] 9.4 用户验证测试新增动作通过配置触发，两个客户端结果一致。
- [ ] 9.5 用户验证测试新增 component 的 snapshot/delta 镜像不会由客户端本地裁决。
- [ ] 9.6 用户验证 runtime effect 添加、过期、移除后的 final component 结果在两个客户端收敛。
