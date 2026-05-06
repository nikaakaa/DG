## 1. 现状审计
- [x] 1.1 列出 `MovementResolveSystem`、`AutoMoveSystem`、`PushOnEnterSystem` 的全部生产代码和测试引用。
- [x] 1.2 列出 `BehaviorIntentKind`、`InferSourceTag`、`InferAbilityTag`、`InferBlockedTags` 的全部调用和测试覆盖。
- [x] 1.3 确认 `StateDrivenRuleExecutionSystem` 已覆盖玩家移动、debug move、auto move、mechanism push、push continuation 的主路径。

## 2. Intent 策略收口
- [x] 2.1 新增 `BehaviorIntentDefinition`，包含 kind、source tag、ability tag、required tags、blocked tags、cancel policy。
- [x] 2.2 新增默认 definition registry，覆盖现有 `Move`、`Push`、`AutoMove`、`MechanismPush`、`DebugMove`。
- [x] 2.3 改造 `BehaviorIntent`，从 registry 读取默认策略，未知 kind 必须失败。
- [x] 2.4 删除 `BehaviorIntent` 内部 `InferSourceTag`、`InferAbilityTag`、`InferBlockedTags` switch。
- [x] 2.5 补 Unity EditMode 测试，证明现有五种 intent 的默认 tag/cancel policy 与当前行为一致。

## 3. System 层结构整理
- [x] 3.1 拆分或迁移 action queue、pending state、commit proposal、state-driven execution 的文件边界。
- [x] 3.2 拆薄 `ProcessPlayerMove`，分离主体解析、目标校验、阻挡查询、push pending、intent 创建和结果回写。
- [x] 3.3 确认 `GameWorld`、`ClientMapWorld`、Fantasy handler 不新增规则裁决逻辑。
- [x] 3.4 刷新 Unity 生成项目文件或确认新增/移动文件已被 Unity csproj 包含。

## 4. Legacy 迁移和删除边界
- [x] 4.1 为旧 `MovementResolveSystem` 覆盖的关键场景建立 state-driven 等价测试。
- [x] 4.2 迁移服务端验证项目中直接依赖 legacy system 的用例。
- [x] 4.3 迁移或隔离客户端本地 `MovementSystem` 对 `MovementResolveSystem` 的依赖。
- [x] 4.4 移除服务端 provider/tick runner 构造参数中的 legacy system 依赖。
- [x] 4.5 在确认无生产引用后，删除或隔离 `Movement/Legacy/Systems.cs`。

## 5. Spec 和验证
- [x] 5.1 更新相关 spec 文本，避免继续把 `MovementResolveSystem` 描述为服务端权威主路径。
- [x] 5.2 运行 `openspec validate refactor-rule-system-hardcoding --strict --no-interactive`。
- [x] 5.3 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 5.4 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 5.5 已补 Unity TestFramework EditMode 覆盖；需要用户在 Unity Editor 手动运行 `IntentArbitrationTests`、`BehaviorArbitrationTests`、`ComponentSystemWorkflowTests`、`PushOnEnterTileTests`。

## 6. 手动验证说明
- [x] 6.1 提供 Play Mode 手动验证步骤：Join、移动、传送带推动、端口连接体冲突、tag 阻断。
- [x] 6.2 提供失败预期：未知 intent kind 不应静默变成玩家来源，legacy system 不应出现在服务端权威主路径。

## 7. 手动验证步骤
- [x] 7.1 打开 `Client/DG_Client`，等待脚本刷新完成后在 Test Runner 运行 EditMode：`IntentArbitrationTests`、`BehaviorArbitrationTests`、`ComponentSystemWorkflowTests`、`PushOnEnterTileTests`、`ClientMapWorldCompositionTests`。
- [x] 7.2 Play Mode 启动服务端和客户端后执行 JoinWorld；预期客户端玩家来自服务端响应或 snapshot/delta，本地默认 runner 不应提前移动权威镜像。
- [x] 7.3 移动玩家到空格；预期服务端 tick 通过 state-driven action / intent / plan / commit 更新坐标，客户端收到成功 response 和/或 WorldDelta 后显示最终坐标。
- [x] 7.4 让玩家站上传送带；预期服务端 tick 生成 mechanism push action 或 intent，推动结果通过 WorldDelta 同步，客户端不靠本地传送带规则推导权威坐标。
- [x] 7.5 用端口连接体制造同 tick 冲突；预期相同 body 的冲突 intent 被 state-driven arbitration 拒绝，连接体保持原坐标。
- [x] 7.6 给玩家加 `BlockPlayerMove`、`StateStunned` 或 `StateRooted` tag 后请求移动；预期 movement action 被 tag 阻断并返回失败 reason。
- [x] 7.7 失败预期：构造未知 `BehaviorIntentKind` 必须抛出明确错误，不应静默 fallback 为 `SourcePlayer`；`MovementResolveSystem`、`AutoMoveSystem`、`PushOnEnterSystem` 不应出现在服务端 provider/tick runner 构造或权威运行主路径。
