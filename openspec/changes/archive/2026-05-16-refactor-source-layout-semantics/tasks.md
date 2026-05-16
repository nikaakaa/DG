## 1. 准备与边界确认
- [x] 1.1 导出现有 `Shared/DG.GameCore`、`Server/Hotfix/AuthoritativeMove`、`ClientWorld`、Unity EditMode 测试文件清单。
- [x] 1.2 确认 `refactor-server-world-storage-to-arch-backend` 的实现状态，避免同时修改同一存储语义。
- [x] 1.3 标出只移动文件、需要拆分文件、必须保留原路径或原 GUID 的文件。

## 2. Shared GameCore 目录重构
- [x] 2.1 建立 Shared GameCore 目标目录骨架。
- [x] 2.2 迁移领域模型、组件、实体、坐标和 snapshot/delta 文件到语义目录。
- [x] 2.3 拆分 `ActionPipeline.cs` 中的 targeting、claim、blocked outcome、strategy registration 和 concrete strategies。
- [x] 2.4 分离 Luban 生成代码、Luban provider、fallback provider 和运行时 registry。
- [x] 2.5 确保 `GameWorld` 仍是规则、存储、空间、dirty 和 snapshot/delta 的公开边界。
- [x] 2.6 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 2.7 拆分 `Domain/Components`，让新增 component 按同名文件落位。
- [x] 2.8 拆分 `RuntimeEffects`，让 effect id、enum、spec、application、instance、store、lifecycle 分文件维护。
- [x] 2.9 拆分 `World` storage 内部类型，保留 `GameWorld` 作为外部入口。
- [x] 2.10 拆分 `Testing` 下 sandbox scenario 和 debug structure block 辅助文件。

## 3. Server Hotfix 目录重构
- [x] 3.1 建立 `Application`、`Runtime`、`Sync`、`Debugging`、`WorldBootstrap`、`ProtocolMapping` 等语义目录。
- [x] 3.2 移动 Handler 之外的应用服务，确保 Handler 只做 session 校验、请求转换和 response/reply。
- [x] 3.3 移动权威 tick、输入队列和同步系统到对应目录。
- [x] 3.4 确保 Fantasy source generator 不需要手动注册修改，且不编辑 `.g.cs`。
- [x] 3.5 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。

## 4. Unity ClientWorld 目录重构
- [x] 4.1 建立 `Mirror`、`Presentation`、`Input`、`DebugTools`、`EditorTools` 等目录并迁移 `.cs` 与 `.meta`。
- [x] 4.2 将 `ClientMapWorld` 等镜像状态代码放到 Mirror 语义下。
- [x] 4.3 将 `ClientAnimationLayer`、`ClientWorldVisuals`、`ClientMapEntity` 放到 Presentation 语义下。
- [x] 4.4 将 runtime debug UI、结构块工具和 port 可视化放到 DebugTools，Editor menu 放到 EditorTools。
- [x] 4.5 保持客户端默认路径只消费 server snapshot/delta，不恢复本地权威移动裁决。

## 5. 测试与验证
- [x] 5.1 增加 Unity TestFramework EditMode 测试，验证 ClientWorld 关键类型仍可装配、镜像状态路径可运行、debug tooling 不直接改权威镜像。
- [x] 5.2 增加或调整 Shared GameCore 语义回归测试，覆盖 action targeting、strategy、claim、commit 拆分后的行为稳定。
- [x] 5.3 增加或调整服务端验证，覆盖 Handler 到权威 tick、WorldDelta 同步路径未因目录迁移改变。
- [ ] 5.4 运行 Unity TestFramework EditMode 测试，不执行 Unity Player build。阻塞：当前同一个 Unity 项目已在另一个 Editor 实例中打开，batchmode 未生成 test result。
- [ ] 5.5 手动端到端验证：启动服务端和两个 Unity 客户端，Join 后执行移动、调试建造、拖拽、删除，确认 B 客户端通过服务端 WorldDelta 同步。

## 6. 收尾
- [x] 6.1 更新受影响文档中的目录说明。
- [x] 6.2 运行 `openspec validate refactor-source-layout-semantics --strict --no-interactive`。
- [x] 6.3 汇总自动测试命令和手动验证步骤给用户。
- [x] 6.4 在 `docs/source-layout.md` 增加新增行为、component、server 类、Unity 类和测试的放置规则。
