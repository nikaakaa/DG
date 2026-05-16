## 1. Proposal Validation
- [x] 1.1 审阅 `runtime-component-results`、`data-driven-runtime-actions`、`client-world-runner` 现有规格。
- [x] 1.2 确认本变更不重复 `add-action-targeting-system` 的 Targeting 范围。
- [x] 1.3 运行 `openspec validate add-effect-application-layer --strict --no-interactive`。

## 2. Effect Data Model
- [x] 2.1 定义 `EffectSpecId`、`EffectSpec`、`EffectKind`、`EffectDurationPolicy`、`EffectStackPolicy`、`EffectRemovePolicy`。
- [x] 2.2 定义 `EffectApplication`，包含 action/source context、target binding、start tick、expire tick、stack key、causality 和 payload。
- [x] 2.3 定义独立 `EffectContribution` / source key 模型，确保每个 effect application 的 component/tag 贡献可独立添加和移除。
- [x] 2.4 定义 first-slice payload：Blocking、AutoMove、Pushable、PortConnector、MovementPermission、Tag。
- [x] 2.5 保留 future Attribute/Stat payload 字段边界，但不实现数值系统。

## 3. Registry And Config
- [x] 3.1 在 Luban schema 中新增独立 `effect_spec` 表和 first-slice 枚举/字段。
- [x] 3.2 新增 effect spec registry，并从 Luban 生成数据加载 first-slice effect specs。
- [x] 3.3 新增 first-slice `effect_spec` 数据行：temporary blocking、auto move、pushable、port、immobile、tag。
- [x] 3.4 为 `ApplyRuntimeEffect` action spec 解析 effect spec id。
- [x] 3.5 增加配置校验：effect id 必须存在，payload 与 effect kind 匹配，duration/stack policy 有效。

## 4. Execution Output
- [x] 4.1 扩展 `ActionExecutionOutput` 支持 `EffectApplication[]`。
- [x] 4.2 实现 `ApplyRuntimeEffect` strategy 从 `TargetData[]` 生成 effect applications。
- [x] 4.3 确认 strategy 不直接调用 `GameWorld.SetComponent`、`GameWorld.AddRuntimeEffect` 或 tag 写入。
- [x] 4.4 覆盖多目标生成多个 effect applications 的最小用例。

## 5. Commit Boundary
- [x] 5.1 扩展 `CommitProposalKind`：`AddRuntimeEffect`、`RemoveRuntimeEffect`、`SetComponentResult`、`AddTag`、`RemoveTag`。
- [x] 5.2 让 `CommitResolver` 按 deterministic 顺序处理 effect/tag/component result proposals。
- [x] 5.3 实现 stack policy：replace、refresh、allow multiple、reject duplicate。
- [x] 5.4 确认 `SetComponentResult` 只写入 runtime contribution/source，不覆盖 final component result。
- [x] 5.5 确认 commit 不按 action/effect readable name 分支。
- [x] 5.6 CommitResult/WorldDelta metadata 能表达 effect added/removed/expired 的必要信息。

## 6. RuntimeEffect And Resolver
- [x] 6.1 让 accepted effect application 写入 `RuntimeEffectStore`。
- [x] 6.2 让 `RuntimeEffectStore` 继续不直接写 `GameWorld` component store。
- [x] 6.3 让 `ComponentStateResolver` 或等价 settlement 只从 active sources/contributions 结算 final component/tag result。
- [x] 6.4 覆盖 duration expire 和 explicit remove 只移除对应 source，剩余 static/runtime source 继续贡献 final result。
- [x] 6.5 覆盖多个 buff/source 添加和删除顺序不同但最终结果一致。
- [x] 6.6 保持 Position、Direction、PlayerControl、tick counter 不进入 resolver 合成。

## 7. Debug And Server Integration
- [x] 7.1 迁移 debug apply runtime effect handler 到 effect/commit path。
- [x] 7.2 迁移 debug remove runtime effect handler 到 effect/commit path。
- [x] 7.3 确认 authoritative tick 每 tick 处理 expire/resolve 的顺序稳定。
- [x] 7.4 Server verification 覆盖 apply、expire、remove、stack、final component read。

## 8. Unity Tests
- [x] 8.1 Unity TestFramework EditMode 覆盖 EffectSpec registry。
- [x] 8.2 EditMode 覆盖 Action 命中目标后生成 EffectApplication。
- [x] 8.3 EditMode 覆盖 AddRuntimeEffect commit 后 resolver 生成 final component。
- [x] 8.4 EditMode 覆盖 tag add/remove effect。
- [x] 8.5 EditMode 覆盖 rules 不查询 RuntimeEffectStore 或 EffectKind。
- [x] 8.6 EditMode 覆盖客户端只消费 server snapshot/delta，不本地解析 effect 命中。

## 9. Automated Validation
- [x] 9.1 `openspec validate add-effect-application-layer --strict --no-interactive`
- [x] 9.2 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- [x] 9.3 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- [x] 9.4 Unity TestFramework EditMode effect 层相关测试。

## 10. Manual End-To-End Verification
- [x] 10.1 用户在 Play Mode 连接服务端权威路径。
- [x] 10.2 用户启动两个客户端，确认 observer 收到一致 WorldDelta。
- [x] 10.3 用户通过调试工具施加 temporary pushable / immobile / auto move / tag effect。
- [x] 10.4 用户确认 effect 生效、过期或移除后两个客户端表现一致。

