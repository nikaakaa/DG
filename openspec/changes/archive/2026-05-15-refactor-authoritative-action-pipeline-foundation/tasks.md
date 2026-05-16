## 1. Proposal Validation

- [x] 1.1 复核 `openspec list`，确认活动变�?`refactor-action-policy-pipeline`、`refactor-auto-move-self-push` 和本变更的依赖关系�?- [x] 1.2 复核 `openspec list --specs`，确认本变更修改�?capability 不重复创建�?- [x] 1.3 复核 `docs/Goal/authoritative-action-effect-system.md` �?ActionContext、TargetData、ExecutionOutput、Claim 管线目标�?- [x] 1.4 复核 `ActionRequests.cs`、`ActionPipeline.cs`、`ActionArbiter.cs`、`StateDrivenRules.cs`、`RulePlanning.cs` 的当前数据流�?- [x] 1.5 运行 `openspec validate refactor-authoritative-action-pipeline-foundation --strict --no-interactive`�?
## 2. Runtime Model

- [x] 2.1 定义 `ActionContext` 或等�?runtime context model，区�?instigator/source/causer、subject entry、target hint、direction、owner、causality、created/ready/cost tick�?- [x] 2.2 �?`ActionRequestAdapter` 作为 `WorldAction` �?`ActionContext` 的导入边界，保留现有行为等价�?- [x] 2.3 增加 context 验证：缺�?spec、source、subject entry、direction/target hint �?fail fast�?- [x] 2.4 增加测试覆盖：player move、mechanism push、debug spawn/remove、AutoMove self push �?context 字段归属�?- [x] 2.5 确认 context 不保存可重新查询�?world fact，不承担 blocked/push/bounce 策略�?
## 3. TargetData

- [x] 3.1 定义 `TargetData`、`TargetDataSet` 或等价模型，表达 target entity、coord、body/subject key、hit cell、hit order、direction、query id�?- [x] 3.2 定义 `TargetingPolicy` 或复用现�?`ActionTargetRule` 扩展�?Self、DirectionCell、FrontCells/Entities 的最小集合�?- [x] 3.3 将现�?`TargetCoordOneStep`、`TargetCoordAny`、`DirectionFromRequest`、`DirectionFromComponent` 包装�?TargetData 输出�?- [x] 3.4 增加 deterministic ordering，禁止多目标 fanout 依赖字典枚举顺序�?- [x] 3.5 增加测试覆盖 Self targeting、DirectionCell targeting、FrontCells/Entities targeting�?
## 4. ExecutionOutput

- [x] 4.1 定义 `ExecutionOutput` 或等价模型，包含 claim candidates、commit candidates、blocked outcome candidates、deferred output candidates、result owner �?success policy�?- [x] 4.2 将现�?move strategy / move claim builder 改为�?context + target data 生成 execution output�?- [x] 4.3 保持 `ExecutionOutput` 不直接修�?`GameWorld`�?- [x] 4.4 保持 claim arbitration、planning、commit 是最终安全层�?- [x] 4.5 增加测试覆盖：execution output 只产出结构化候选，不直接写世界状态�?
## 5. Multi Target Claims

- [x] 5.1 支持多个 TargetData 生成多个 claim candidates�?- [x] 5.2 支持“面前所有对象移动”的最小权威闭环：front target data fanout、每个目标产生移�?claim、仲裁、规划、提交�?- [x] 5.3 增加 same target / same subject dedupe 或冲突规则入口，保持 deterministic result�?- [x] 5.4 增加 all-or-nothing 默认策略：任一 required claim 失败�?unit 不报告整体成功�?- [x] 5.5 增加 partial success 数据入口�?guard：未显式配置 partial 时不得部分提交并报告成功�?- [x] 5.6 增加测试覆盖：多目标全部成功、多目标部分阻塞默认失败、partial policy 未实现时 fail fast 或明确拒绝�?
## 6. AutoMove Self Push Integration

- [x] 6.1 �?AutoMove self push context 表达�?source/instigator/subject entry 都指�?AutoMove entity�?- [x] 6.2 �?self push �?target data 接入 Self �?DirectionCell policy，不通过 action name 特判�?- [x] 6.3 �?self push blocked direction reversal 接入 execution output / blocked policy / commit rule，结果归属同一 context�?- [x] 6.4 确认不新�?child feedback、旧 pending parent/child �?parent retry�?- [x] 6.5 增加测试覆盖：self push 成功、直�?block 反向、pushable 成功不反向、当�?action 边界内下�?block 反向、跨 tick deferred output 不隐式回写原 source�?
## 7. Automated Tests

- [x] 7.1 Unity TestFramework EditMode：ActionContext 字段归属�?validation�?- [x] 7.2 Unity TestFramework EditMode：TargetData Self / DirectionCell / FrontCells/Entities�?- [x] 7.3 Unity TestFramework EditMode：ExecutionOutput 生成 claims/deferred/blocked candidates�?- [x] 7.4 Unity TestFramework EditMode：多 TargetData fanout �?claims�?- [x] 7.5 Unity TestFramework EditMode：all-or-nothing 默认策略�?- [x] 7.6 Unity TestFramework EditMode：partial success 未显式支持时 fail fast�?- [x] 7.7 Unity TestFramework EditMode：AutoMove self push 不依�?pending parent/child�?- [x] 7.8 扫描测试：权威规则路径不新增普�?action-name 分支，不复活 `PendingRuleStates` 作为 push 默认路径�?
## 8. Server / Shared Validation

- [x] 8.1 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`�?- [x] 8.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`�?- [x] 8.3 确认 player move、mechanism push、AutoMove self push、multi target front move 最小场景都走服务端权威结果�?- [x] 8.4 运行 `openspec validate refactor-authoritative-action-pipeline-foundation --strict --no-interactive`�?
## 9. Manual Verification

> 由用户手动执行，不要�?AI 代替完成�?
- [x] 9.1 �?Unity Play Mode 中连接服务端权威路径�?- [x] 9.2 启动两个客户端并注册 observer�?- [x] 9.3 验证 AutoMove self push 成功、撞墙反向、推可推动对象成功、推对象撞墙反向�?- [x] 9.4 验证“面前所有对象移动”的最小场景，两个客户端收到一�?`WorldDelta`�?- [x] 9.5 确认客户端没有本地裁�?ActionContext、TargetData、ExecutionOutput、claim、blocked result 或最终权威坐标�?