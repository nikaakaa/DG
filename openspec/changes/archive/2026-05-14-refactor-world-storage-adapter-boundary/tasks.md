## 1. Proposal Validation

- [x] 1.1 复核 `openspec list`，确认本变更不并入 `refactor-authoritative-world-data-oriented-storage`、`refactor-action-policy-pipeline` 或 `refactor-auto-move-self-push`。
- [x] 1.2 复核 `shared-gamecore-entity-rules` 中 `ECS Storage Abstraction Boundary` 和 `Runtime Storage Observability` 要求。
- [x] 1.3 复核 `docs/Goal/world-storage-next-step.md` 和 `docs/Goal/authoritative-action-effect-system.md`，确认本变更只处理 storage adapter 边界。
- [x] 1.4 运行 `openspec validate refactor-world-storage-adapter-boundary --strict --no-interactive`。

## 2. Boundary Audit

- [x] 2.1 复核 `GameWorld` 对 `WorldDataStorage` 的所有调用点。
- [x] 2.2 复核 `WorldDataStorage` 当前实体、组件、mask、query cache 职责。
- [x] 2.3 确认 `SpatialEntityIndex`、`SpatialDirtyTracker`、`DirtyWorldJournal`、`RuntimeEffectStore` 不进入 storage adapter。
- [x] 2.4 确认 `Rules/*`、server runner、sync system 和 Unity client mirror 不直接依赖 concrete storage。

## 3. Storage Adapter Contract

- [x] 3.1 新增或整理内部 `IWorldDataStorage` 合同。
- [x] 3.2 合同只包含当前 `GameWorld` 已经需要的实体、组件和组件组合查询操作。
- [x] 3.3 合同不暴露 row、pool、mask、cache、generation 或第三方 ECS 类型。
- [x] 3.4 合同保持 internal，不成为规则层 public API。

## 4. Indexed Adapter

- [x] 4.1 将当前 concrete storage 明确为 indexed/dense-pool adapter。
- [x] 4.2 保留现有 `EntityRegistry`、`ComponentTypeRegistry`、`ComponentPool<T>`、component mask 和 `QueryCache` 语义。
- [x] 4.3 保持 `QueryEntities` ordering contract 不变。
- [x] 4.4 保持 entity create/remove/recreate 后旧 id 不会误读新实体。

## 5. GameWorld Integration

- [x] 5.1 将 `GameWorld` 对 concrete storage 的直接依赖改为 adapter 合同。
- [x] 5.2 保持 `GameWorld` 默认构造路径使用 indexed adapter。
- [x] 5.3 如需测试注入，仅开放最小 internal/test 入口，不暴露给规则层。
- [x] 5.4 确认 `GameWorld` public API 不新增 storage-specific 方法。
- [x] 5.5 确认 `GameWorld` 仍统一负责 spatial、dirty、snapshot、delta 和 runtime effect final component resolution。

## 6. Boundary Guards

- [x] 6.1 增加扫描或测试，证明 `Rules/*` 不引用 `IWorldDataStorage`、indexed adapter、pool、mask 或 query cache。
- [x] 6.2 增加扫描或测试，证明 server Hotfix 层不引用 storage adapter 或 pool。
- [x] 6.3 增加扫描或测试，证明 Shared GameCore 不新增 Arch、Unity DOTS 或第三方 ECS 依赖。
- [x] 6.4 增加测试，证明 adapter 化前后 `QueryAutoMove`、`QueryPushOnEnter`、blocking/collider/pushable 空间查询语义不变。

## 7. Automated Tests

- [x] 7.1 Shared 测试：entity add/remove/recreate 与旧 id 隔离。
- [x] 7.2 Shared 测试：component set/update/remove 与 query cache 命中一致。
- [x] 7.3 Shared 测试：snapshot/delta 不暴露 storage adapter 细节。
- [x] 7.4 Shared 测试：observation counters 仍区分 enumeration、component lookup、spatial query、snapshot 和 delta。
- [x] 7.5 Server verification：join、合法移动、非法移动、auto move、push、connected body、debug edit、runtime effect 全部通过。
- [x] 7.6 Unity EditMode：客户端 mirror 应用 snapshot/delta 后最终状态一致。

## 8. Validation

- [x] 8.1 运行 `openspec validate refactor-world-storage-adapter-boundary --strict --no-interactive`。
- [x] 8.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 8.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 8.4 运行相关 Unity TestFramework EditMode 测试。

## 9. Manual Verification

> 由用户手动执行，不要求 AI 代替完成。

- [x] 9.1 启动服务端权威路径。
- [x] 9.2 在 Unity Play Mode 启动第一个客户端并 JoinWorld。
- [x] 9.3 启动第二个客户端并注册 observer。
- [x] 9.4 执行玩家移动、自动移动、机关推动、debug spawn/move/remove、runtime effect。
- [x] 9.5 确认两个客户端收到同一服务端 `WorldDelta` 序列。
- [x] 9.6 确认客户端没有本地裁决权威规则或最终坐标。
