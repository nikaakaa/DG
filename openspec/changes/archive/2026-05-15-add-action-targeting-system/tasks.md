## 1. Proposal Validation

- [x] 1.1 阅读 `docs/Goal/authoritative-action-effect-system.md` 的 Targeting / Query 层，确认本变更只落 targeting 底层切片。
- [x] 1.2 对齐活动变更 `refactor-authoritative-action-pipeline-foundation`，确认复用现有 `ActionContext`、`ActionTargetData`、`ActionExecutionOutput`。
- [x] 1.3 运行 `openspec validate add-action-targeting-system --strict --no-interactive`。

## 2. Targeting 数据模型

- [x] 2.1 新增 target selector 接口和 selector registry。
- [x] 2.2 新增 `TargetingSpecId`、`TargetingSpec`、`TargetSelectorId`、`TargetDirectionSource`、`TargetOrderingPolicy`。
- [x] 2.3 新增 `TargetFilterSpec`、`TargetFilterCondition`、`TargetFilterSubject`。
- [x] 2.4 扩展 `ActionSpec` 或 registry，让 action 能引用解析后的 targeting/filter policy。
- [x] 2.5 保留现有 `ActionTargetRule` 作为迁移兼容入口，不让新普通行为继续依赖新增枚举分支。
- [x] 2.6 增加模型验证，缺失 targeting/filter/selector 引用时显式失败。

## 3. TargetingSystem 底层

- [x] 3.1 新增只读 `TargetingSystem`，输入 `GameWorld`、`ActionContext`、`ActionSpec`、`TargetingSpec`。
- [x] 3.2 实现 selector registry mapping，通过解析后的 selector id 找到具体 selector 类。
- [x] 3.3 迁移 `Self` 查询为基础 selector，输出 `TargetData` 且不修改 world。
- [x] 3.4 迁移 `DirectionCell` 查询为基础 selector，缺失方向时返回稳定失败原因。
- [x] 3.5 迁移 `TargetCoordOneStep` 和 `TargetCoordAny` 查询为基础 selector，保留 too-far / invalid-direction 语义。
- [x] 3.6 增加 target filter 执行，支持 position、collider、pushable、blocking、tag 条件。
- [x] 3.7 明确空目标策略，区分失败、返回 primary cell、返回空集合。
- [x] 3.8 删除或收敛 `ActionTargetSelector` 中新增普通行为分支的需求，让它只作为 adapter 或委托到 `TargetingSystem`。

## 4. 配置和 Luban

- [x] 4.1 扩展 Luban schema，表达 targeting spec、selector id 和 target filter spec。
- [x] 4.2 更新 Excel/JSON 数据，覆盖 player move、auto move、mechanism push、debug move 的基础 targeting 映射。
- [x] 4.3 运行 Luban 导出，生成 targeting/filter 数据。
- [x] 4.4 更新 provider，把可读 targeting/filter/selector 名称解析成 runtime id 或 enum。
- [x] 4.5 增加配置校验，未知 targeting/filter/selector 引用必须失败。

## 5. Execution / Claim 消费

- [x] 5.1 让 action arbitration 从 `TargetingSystem` 获取 `TargetData[]`。
- [x] 5.2 让 move execution/claim fanout 消费同一组 `TargetData[]`。
- [x] 5.3 保持 Targeting 不生成 claim、plan、commit、deferred output。
- [x] 5.4 验证 target fanout 仍经过 arbitration/planning/commit。
- [x] 5.5 验证 all-or-nothing 是默认策略，partial success 只作为显式后续入口保留。

## 6. Unity TestFramework EditMode

- [x] 6.1 测试 selector registry 能由配置 id 找到对应 selector。
- [x] 6.2 测试 `Self` targeting 输出 source entity target data。
- [x] 6.3 测试 `DirectionCell` targeting 输出目标格和方向。
- [x] 6.4 测试 `TargetCoordOneStep` 超距失败语义不变。
- [x] 6.5 测试 target filter 只返回满足 final component/tag 的目标。
- [x] 6.6 测试 Targeting 不修改 `GameWorld`、dirty、snapshot/delta。
- [x] 6.7 测试 action 改名但 targeting/filter/selector policy 相同，结果等价。
- [x] 6.8 测试未知 targeting/filter/selector id 显式失败。

## 7. Shared / Server 验证

- [x] 7.1 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 7.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 7.3 覆盖玩家移动、AutoMove、PushOnEnter 的服务器规则验证。
- [x] 7.4 确认 `Shared/DG.GameCore` 不引用 UnityEngine、Fantasy、protocol generated types。

## 8. 用户手动端到端验证

- [ ] 8.1 用户在 Play Mode 连接服务端，验证玩家单格移动仍由服务端裁决。
- [ ] 8.2 用户验证 AutoMove self target 或 direction component 行为仍通过服务端 delta 同步。
- [ ] 8.3 用户验证 PushOnEnter 机关推动仍由服务端 targeting/arbitration/commit 后同步。
- [ ] 8.4 用户用双客户端观察基础 targeting 行为，确认两个客户端收到一致 `WorldDelta`。
- [ ] 8.5 手动报告区分 OpenSpec validate、自动测试、server verification、Play Mode / 双客户端结果。

## 9. 后续 Selector 扩展

- [ ] 9.1 后续单独 proposal 增加 `FrontLineTargetSelector` 或 `FrontEntitiesTargetSelector`。
- [ ] 9.2 后续单独 proposal 增加 box/circle/shape targeting selector。
- [ ] 9.3 后续 selector 扩展必须复用本变更的 selector registry、TargetingSpec、TargetFilter 和 TargetData 输出契约。
