## Context
项目已经有清晰的运行时架构：Shared GameCore 是纯 C# 规则真相，Fantasy Server 是权威运行外壳，Unity ClientWorld 是服务端结果的镜像和展示层。现有目录已经部分接近目标，但仍存在几个可读性问题：

- `Shared/DG.GameCore/Rules/Actions/ActionPipeline.cs` 聚合了 targeting、claim、blocked outcome、strategy registration 和具体策略等多个语义。
- `Shared/DG.GameCore/Config` 同时包含稳定运行时配置模型、Luban 适配、fallback provider 和生成代码。
- `Server/Hotfix/AuthoritativeMove/Infrastructure` 同时放了 observer、debug edit 和 multiplayer entity 管理。
- `ClientWorld` 目录已经存在，但 namespace 仍是 `DG.Map`，并且 mirror、presentation、debug tooling、networking 语义需要更直观。

## Goals
- 目录名直接表达模块责任，不靠开发者记忆文件内容。
- 文件粒度跟语义单元一致，尤其是规则管线、配置适配、客户端镜像和调试工具。
- 保持运行时行为稳定，迁移不改变权威规则和同步协议。
- 让测试文件位置与被验证模块对应，便于后续按模块运行和维护。

## Non-Goals
- 不重写规则管线。
- 不引入新的 ECS、DI 框架或代码生成体系。
- 不改变 Fantasy Handler 注册机制或协议生成文件。
- 不把 Unity 客户端提升为规则权威。

## Decisions
- Shared GameCore 采用语义层级目录：`Domain`、`Configuration`、`World`、`Spatial`、`Snapshots`、`Rules`、`RuntimeEffects`、`Testing`。保留 `DG.GameCore` 命名空间，避免大规模 using churn。
- `Rules` 下按职责拆分：`Actions/Intake`、`Actions/Targeting`、`Actions/Arbitration`、`Actions/Execution`、`Actions/Strategies`、`Planning`、`Commit`、`Connectivity`。文件移动和拆分必须保持公开 API 行为不变。
- Luban 生成代码继续在明确的 `Generated` 子树中，运行时代码只能通过 provider/registry 读取，不直接依赖生成表类型。
- Fantasy Hotfix 采用应用层语义：`Handlers`、`Application`、`Runtime`、`Sync`、`Debugging`、`WorldBootstrap`、`ProtocolMapping`。Fantasy namespace 可保留为 `Fantasy`，但目录表达职责。
- Unity ClientWorld 采用客户端职责语义：`Bootstrap`、`Networking`、`Mirror`、`Presentation`、`Input`、`DebugTools`、`EditorTools`。迁移时优先保持 serialized `.meta` GUID，避免场景和 prefab 断引用。

## Risks / Trade-offs
- Unity 文件移动可能丢失引用。缓解：移动 `.cs` 与 `.meta` 成对迁移，迁移后运行 EditMode 测试并打开 Play Mode 手动验证。
- 大文件拆分可能制造 API churn。缓解：先按内部类型职责拆分，不改外部行为；必要时保留 facade 类型。
- 与 `refactor-server-world-storage-to-arch-backend` 可能同时触碰 `GameWorld` 和 server world provider。缓解：本变更只定义目录和文件归属，不改变 storage backend 语义；实现时先确认该 change 是否已归档或合并。

## Migration Plan
1. 记录当前文件清单和引用热点，标出要迁移的文件和保留位置。
2. 先迁移 Shared GameCore，确保 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore` 通过。
3. 迁移 Server Hotfix，确保服务端验证项目通过。
4. 迁移 Unity ClientWorld 和 EditMode 测试，确保 Unity TestFramework EditMode 覆盖通过。
5. 做一次手动 Play Mode 双客户端验证，确认服务端权威同步和调试工具未被目录迁移破坏。

## Open Questions
- 是否允许在同一变更中把 Unity 客户端 namespace 从 `DG.Map` 改成 `DG.ClientWorld`？默认建议暂不改 namespace，等目录迁移稳定后再单独提 namespace 清理。
- 是否要把 `ActionPipeline.cs` 一次拆到目标粒度？默认建议拆到语义清楚且测试能覆盖的粒度，若冲突过大则按 `Targeting`、`Strategy`、`Arbitration` 三个独立批次执行。
