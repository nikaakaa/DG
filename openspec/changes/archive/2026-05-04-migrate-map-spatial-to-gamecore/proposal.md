# Change: 迁移地图空间底层到共享 GameCore

## Why
当前服务端 `DG.GameCore.GameWorld` 使用独立的 `Dictionary<GridCoord, HashSet<long>>` 做格子索引，而 Unity 客户端已有 `World`、`ChunkStore`、`EntityTracker`、`DirtyTracker` 等空间分块能力。两套空间底层会阻碍客户端预测、服务端权威规则复用和后续 AOI/分块查询统一。

## What Changes
- 将 Unity 侧可共享的空间底层能力迁移到 `Shared/DG.GameCore`，保持纯 C#，不依赖 `UnityEngine` 或 `Fantasy`。
- Unity 客户端通过本地 package 引用 `Shared/DG.GameCore`，不通过复制源码或手动维护 Unity 生成 `.csproj` 的方式接入。
- 用 `GridCoord` 作为共享坐标类型，Unity 侧仅在边界做 `Vector2Int` 与 `GridCoord` 转换。
- 让 `GameWorld` 使用共享空间索引替代当前临时 `spatial` 字典，并保持组件驱动的移动、阻挡、反弹规则不变。
- 让 Unity 客户端能够引用并执行 `DG.GameCore` 的最小规则测试，为后续预测/校正做准备。
- 迁移完成后去除 Unity 旧的规则/空间 World 设计，Unity 侧只保留表现、输入、网络和调试外壳。
- 保留 Unity 表现层、编辑器调试和 Fantasy 网络层在各自外壳中，不把 `MonoBehaviour`、`Session` 或协议生成类型迁入 Shared。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `map-runtime-foundation`, `client-world-runner`
- Affected code:
  - `Shared/DG.GameCore/*`
  - `Client/DG_Client/Assets/Scripts/Map/Spatial/*`
  - `Client/DG_Client/Assets/Scripts/Map/Runtime/*`
  - `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime/*`
  - `Server/Hotfix/AuthoritativeMove/*`
  - Unity TestFramework EditMode tests
