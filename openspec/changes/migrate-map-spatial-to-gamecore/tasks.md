## 1. Shared 空间底层
- [x] 1.1 梳理 Unity `Map/Spatial` 中可共享的纯逻辑类型和必须保留在 Unity 的表现/编辑器类型。
- [x] 1.2 在 `Shared/DG.GameCore` 中建立空间基础命名和文件边界。
- [x] 1.3 迁移坐标 key 生成能力，使用 `GridCoord` 计算 cell key、chunk key、chunk coord、local coord。
- [x] 1.4 迁移或重建 `Chunk`、`Cell`、`ChunkStore` 的纯 C# 版本。
- [x] 1.5 迁移或重建 entity 空间索引，支持按 cell、chunk 和 target 查询。
- [x] 1.6 迁移或重建 dirty cell/chunk tracker。

## 2. GameWorld 接入
- [x] 2.1 用共享空间索引替换 `GameWorld` 当前临时 `spatial` 字典。
- [x] 2.2 保持 `PositionComponent` 与 `ColliderComponent` 变更时空间索引同步。
- [x] 2.3 保持 `GetEntitiesAt`、`CanEnterNewEntity` 和移动裁决对外行为不变。
- [x] 2.4 确认 `RemoveEntity` 会从组件存储和空间索引中同时清理 entity。

## 3. Unity 客户端接入
- [x] 3.1 将 `Shared/DG.GameCore` 作为本地 Unity package 接入客户端工程。
- [x] 3.2 添加 `GridCoord` 与 `Vector2Int` 的客户端边界转换。
- [x] 3.3 让客户端测试代码能创建 `GameWorld` 并运行 `MovementResolveSystem`。
- [x] 3.4 将客户端旧 `World` 的规则职责替换为 Shared GameCore。
- [x] 3.5 删除不再需要的 Unity-only 空间规则实现，保留必要表现、输入、网络和调试代码。

## 4. 测试与验证
- [x] 4.1 添加 Shared GameCore 空间 key、负坐标 chunk/local 转换测试。
- [x] 4.2 添加 Shared GameCore entity 注册、移动、移除、按 target 查询测试。
- [x] 4.3 添加移动阻挡和反弹规则在新空间索引下的验证。
- [x] 4.4 添加 Unity TestFramework EditMode 测试，验证 Unity 客户端可执行 Shared GameCore 移动规则。
- [x] 4.5 运行 `dotnet build Shared\DG.GameCore\DG.GameCore.csproj -v minimal`。
- [x] 4.6 运行 `dotnet run --project Server\Tests\AuthoritativeMoveVerification\AuthoritativeMoveVerification.csproj`。
- [ ] 4.7 运行 Unity TestFramework EditMode 测试。
- [ ] 4.8 手动端到端验证：启动服务端，打开 Unity demo，确认玩家移动、球自动移动、球碰玩家/阻挡体反弹、多个客户端同步仍然正常。
