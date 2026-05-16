## 1. 目录映射
- [x] 1.1 列出 `Shared/DG.GameCore/Rules` 下所有 `.cs` 文件，标注目标目录。
- [x] 1.2 标注哪些文件只移动，哪些文件需要拆分。
- [x] 1.3 标注所有自动生成文件和生成器输出路径。
- [x] 1.4 标注所有实现阶段不能手改的 `.meta` 文件。

## 2. ActionRuntime 骨架
- [x] 2.1 创建 `Shared/DG.GameCore/ActionRuntime` 目录结构。
- [x] 2.2 移动 action spec、id、policy model 到 `ActionRuntime/Specs`。
- [x] 2.3 移动 action request、world action、queue/lifecycle 类型到 `ActionRuntime/Requests`、`Queue`、`Lifecycle`。
- [x] 2.4 移动 `StateDrivenRules` 到 `ActionRuntime/Execution`。
- [x] 2.5 保持现有 namespace，确认引用不因目录变化中断。

## 3. Strategy 和生成代码
- [x] 3.1 将 strategy interface、attribute、registry、generator helper、实现类从单文件拆出。
- [x] 3.2 将 strategy contracts 放到 `ActionRuntime/Strategies/Contracts`。
- [x] 3.3 将手写 strategy implementation 放到 `ActionRuntime/Strategies/Implementations`。
- [x] 3.4 将自动生成的 strategy registration 放到 `ActionRuntime/Generated`。
- [x] 3.5 更新 Unity 编辑器生成器输出路径。
- [x] 3.6 增加或调整 Unity EditMode 测试，证明生成注册仍能注册所有策略。

## 4. Targeting、Gating、Subject
- [x] 4.1 将 targeting contracts、registry、system、data model、selector implementation 分文件。
- [x] 4.2 将 targeting selector implementation 放到 `ActionRuntime/Targeting/Selectors`。
- [x] 4.3 将 target filter 和 component fact query 相关文件放到 `ActionRuntime/Targeting/Filters` 或 `Gating/Conditions`。
- [x] 4.4 将 action gating 放到 `ActionRuntime/Gating`。
- [x] 4.5 将 subject selector 放到 `ActionRuntime/Subjects`。
- [x] 4.6 用 EditMode 测试覆盖 self、direction cell、front entities、component filter。

## 5. Claims、Blocking、Planning
- [x] 5.1 将 claim model、claim builder、claim arbitration 放到 `ActionRuntime/Claims`。
- [x] 5.2 将 push vector composition 放到 `ActionRuntime/Claims` 或 `Blocking/Policies`，并在目录映射中说明归属。
- [x] 5.3 将 blocking contact collection、blocked policy matching、blocked outcome execution 拆到 `ActionRuntime/Blocking`。
- [x] 5.4 将 derive、bounce、reject、noop 等业务 outcome 放到 `ActionRuntime/Blocking/Outcomes`。
- [x] 5.5 保留 executor 只做 outcome dispatch，不保留普通行为细节。
- [x] 5.6 将 planning 和 body capability resolver 放到 `ActionRuntime/Planning`。
- [x] 5.7 用 EditMode 测试覆盖 push handoff、bounce block、reject、noop、connected body blocker。

## 6. Commit 和 World 边界
- [x] 6.1 将 commit proposal/result/context/registry 放到 `ActionRuntime/Commit`。
- [x] 6.2 将具体 commit handler 放到 `ActionRuntime/Commit/Handlers`。
- [x] 6.3 将 world storage、spatial、snapshot、delta 代码归入 `World` 目录。
- [x] 6.4 确认 `World` 不依赖普通 action id、strategy id、blocked outcome id。
- [x] 6.5 用测试覆盖 move、spawn、remove、component result、effect result commit。

## 7. RuntimeEffects、Domain、Configuration
- [x] 7.1 将 component、entity id、稳定值对象归入 `Domain`。
- [x] 7.2 将 runtime effect spec、store、settlement 放到 `RuntimeEffects`。
- [x] 7.3 将 Luban provider/importer 放到 `Configuration/Luban`。
- [x] 7.4 保持 `Configuration/Generated` 只承载 Luban 生成表。
- [x] 7.5 确认生成代码目录没有人工业务逻辑。

## 8. 验证
- [x] 8.1 运行 `openspec validate refactor-shared-gamecore-source-layout --strict --no-interactive`。
- [x] 8.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj`。
- [x] 8.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj`。
- [x] 8.4 运行 Unity TestFramework EditMode 测试。
- [ ] 8.5 用户手动 Play Mode 或两客户端验证 player move、auto move、push/bounce、debug spawn/remove、snapshot/delta。
- [x] 8.6 检查没有手动修改 `.meta` 文件。
