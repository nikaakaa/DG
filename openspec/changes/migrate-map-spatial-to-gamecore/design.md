## Context
当前项目已经把实体、组件、移动规则和服务端权威世界收敛到 `Shared/DG.GameCore`。但是空间底层仍然分裂：

- 服务端 `GameWorld` 使用临时 `GridCoord -> EntityId` 字典。
- Unity 客户端 `DG.Map.World` 使用 `ChunkStore`、`EntityTracker`、`DirtyTracker` 和 `MapCoordinate`。
- Unity 客户端暂时只消费服务端 snapshot/delta，不执行共享规则，因此还不能支撑可靠预测。

这次变更的目标是迁移“纯空间底层”到 Shared，而不是一次性实现完整客户端预测。

## Goals / Non-Goals
- Goals:
  - Shared GameCore 拥有坐标 key、chunk/cell、空间 entity 索引、dirty cell/chunk 的纯 C# 实现。
  - 服务端 `GameWorld` 使用 Shared 空间索引维护位置、碰撞实体和格子查询。
  - Unity 客户端通过本地 package 引用 `DG.GameCore`，并通过 Unity TestFramework 跑同一套最小移动规则。
  - Unity 旧空间/规则 World 设计在迁移后去除，Unity 侧只保留表现、输入、网络、调试和必要坐标转换。
- Non-Goals:
  - 不实现完整预测、回滚、重放或插值。
  - 不修改网络协议格式，除非编译适配确实需要。
  - 不把 Unity `MonoBehaviour`、`Vector2Int`、`GameObject`、`Debug.Log` 放入 Shared。
  - 不把 Fantasy `Session`、Handler、RPC、协议生成类型放入 Shared。
  - 不在本变更中重做 EntityConfig 的组件列表化。

## Decisions
- Decision: Shared 使用 `GridCoord` 作为唯一规则坐标。
  - Alternatives considered: 直接迁移 Unity `Vector2Int` 版本。拒绝，因为会让服务端 GameCore 依赖 UnityEngine。
- Decision: Unity 通过本地 package 引用 Shared GameCore。
  - Alternatives considered: 手动修改 Unity 生成 `.csproj` 或复制 Shared 源码到 `Assets`。拒绝，因为 `.csproj` 会被 Unity 重生成，复制源码会产生第二份规则实现。
- Decision: 先迁移纯空间底层，再做预测。
  - Alternatives considered: 同时实现预测/校正。拒绝，因为会把空间迁移、客户端状态同步和命令确认混在一起，验证边界过大。
- Decision: `GameWorld` 继续作为规则 facade，内部组合共享空间服务。
  - Alternatives considered: 直接把 Unity `DG.Map.World` 搬到 Shared。拒绝，因为它带有客户端历史形态和 Unity 类型。
- Decision: 迁移后去除 Unity 旧空间/规则 World 设计。
  - Alternatives considered: 长期保留 Unity 旧 `World` 作为并行适配层。拒绝，因为会形成两套规则真相，后续预测和校正容易漂移。
- Decision: 保留网络 snapshot/delta 作为服务端到客户端的权威投影。
  - Alternatives considered: 立刻做通用组件同步。拒绝，因为当前目标是空间底层共享，不扩大到协议结构重做。

## Risks / Trade-offs
- Risk: 本地 package 路径或 asmdef 配置不正确会导致 Unity 引用失败。
  - Mitigation: 明确 package 接入方式，并用 Unity TestFramework 与 `dotnet build` 验证。
- Risk: 迁移空间索引时破坏移动阻挡和反弹规则。
  - Mitigation: 先保留现有 `MovementResolveSystem` 行为测试，再增加共享空间索引测试。
- Risk: Shared 同时承担服务端和客户端需求后边界变宽。
  - Mitigation: 明确 Shared 只允许纯规则、纯数据和确定性查询，不允许 Fantasy/Unity 运行时类型。

## Migration Plan
1. 在 Shared 中新增或迁移纯空间基础类型，使用 `GridCoord` 替代 `Vector2Int`。
2. 将 `GameWorld` 的临时 `spatial` 字典替换为共享空间索引服务。
3. 将服务端现有权威移动验证迁到新空间索引下并保持通过。
4. 通过本地 package 让 Unity 客户端引用 `DG.GameCore`，新增 Unity TestFramework 测试验证共享规则可在客户端运行。
5. 删除或替换 Unity 客户端旧 `World`、`ChunkStore`、`EntityTracker`、`DirtyTracker` 的规则职责，避免出现两套规则真相。

## Open Questions
- `EntityTarget` 是否迁入 Shared 并与 `EntityTarget`/`EntityTargetId` 合并，需要在实现时根据现有协议和客户端表现层影响决定。
