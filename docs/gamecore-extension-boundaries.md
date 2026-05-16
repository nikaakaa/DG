# GameCore 扩展边界

## 新增普通行为
- 能用现有策略表达时，只改 Luban action policy 数据、导出配置并补 Unity EditMode 测试。
- 需要新底层策略时，新增 `IActionStrategy` 小类，声明 `ActionStrategyId`，加入显式 registration。
- 不在 `StateDrivenRuleExecutionSystem` 里按 action 名字或玩法名字分支。

## 新增 commit 语义
- 新增 `ICommitProposalHandler`，注册到 `CommitHandlerRegistry`。
- `CommitResolver.Resolve` 只排序、准备共享上下文并调 handler。
- 移动占位、移动组、阻挡验证继续走共享 move handler。

## 新增静态 component
- 新增 `ComponentId` 和 applicator 注册。
- archetype 通过 component id 引用组件。
- `EntityBuilder` 只遍历 component id 并调用 `ComponentApplicationRegistry`。

## 新增可查询 component
- 新增 `ComponentFactQueryRegistry` 注册项。
- target filter 和 blocked condition 只按 component id 查询 final component fact。

## 新增 runtime component result
- 新增 `ComponentResultId` 和 `IComponentResultResolver`。
- `ComponentStateResolver` 只收集 static/runtime source、排序并调用 resolver。
- tag source settlement 保持独立，不混入 component result switch。

## 新增 snapshot/delta 同步 component
- 新增 `IEntitySnapshotProjector` 和 `IEntitySnapshotApplier`。
- `GameWorld.CreateSnapshot` 和 `GameWorld.ApplySnapshot` 只调 `SnapshotComponentRegistry`。
- 未注册同步投影的 component 默认只保留在服务端权威状态。
