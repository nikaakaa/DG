## 1. 底层拆分
- [x] 1.1 新增 `ChunkStore` 类，用于管理已加载 chunk。
- [x] 1.2 将 `GetOrCreateChunk` 迁入 `ChunkStore`。
- [x] 1.3 将 `TryGetChunk` 迁入 `ChunkStore`。
- [x] 1.4 将 `HasChunk` 迁入 `ChunkStore`。
- [x] 1.5 将 `GetOrCreateCell` 迁入 `ChunkStore`。
- [x] 1.6 将 `TryGetCell` 迁入 `ChunkStore`。
- [x] 1.7 将 `GetChunksAround` 迁入 `ChunkStore`。
- [x] 1.8 更新 `World`，让 chunk 操作委托给 `ChunkStore`。
- [x] 1.9 保持 `EntityTracker` 独立于 chunk storage 和 chunk 数据。

## 2. 坐标与 key 边界
- [x] 2.1 在 `MapCoordinate` 或等价类型中集中 chunk key 生成，并返回 packed long。
- [x] 2.2 在 `MapCoordinate` 或等价类型中集中 cell key 生成，并返回 packed long。
- [x] 2.3 确认负坐标的 chunk/local/key 转换结果保持一致。
- [x] 2.4 确认外部调用点不直接复制 chunk/cell key 计算逻辑。
- [x] 2.5 确认 chunk、cell、dirty、entity 空间索引不再新增 `Vector2Int` 字典 key。

## 3. Dirty tracker
- [x] 3.1 新增 `DirtyTracker` 类，用于管理 changed cells 和 changed chunks。
- [x] 3.2 将 dirty cell 集合迁入 `DirtyTracker`。
- [x] 3.3 将 dirty chunk 集合迁入 `DirtyTracker`。
- [x] 3.4 将 mark cell 操作迁入 `DirtyTracker`。
- [x] 3.5 将 mark chunk 操作迁入 `DirtyTracker`。
- [x] 3.6 将 clear 操作迁入 `DirtyTracker`。
- [x] 3.7 更新 `World`，让 dirty 操作委托给 `DirtyTracker`。
- [x] 3.8 确认 dirty tracker 不被设计成玩家视野同步的唯一来源。

## 4. Entity target 分区索引
- [x] 4.1 定义 `EntityTarget` enum，至少覆盖 all、player、monster、object。
- [x] 4.2 为 `EntityTracker` 增加按 target 分区的 entity 集合。
- [x] 4.3 为 `EntityTracker` 增加按 target 分区的 cell entity 索引。
- [x] 4.4 为 `EntityTracker` 增加按 target 分区的 chunk entity 索引。
- [x] 4.5 更新 entity register，使 entity 加入 all target 和自身分类 target。
- [x] 4.6 更新 entity move，使所有相关 target 的 cell/chunk 索引同步迁移。
- [x] 4.7 更新 entity unregister，使所有相关 target 的索引同步移除。
- [x] 4.8 保持按 id 查询不依赖 target。
- [x] 4.9 增加按 target 查询 cell entities 的 API。
- [x] 4.10 增加按 target 查询 chunk entities 的 API。
- [x] 4.11 增加按 target 查询范围 entities 的 API。
- [x] 4.12 定义无 target 查询入口等价于 all target。

## 5. 观察者视野差量
- [x] 5.1 定义 visibility delta 结果，包含 entered 和 left。
- [x] 5.2 增加根据旧坐标、新坐标、cell 视野范围和 target 计算差量的 API。
- [x] 5.3 当旧坐标和新坐标处于相同视野覆盖范围时，差量为空。
- [x] 5.4 当 observer 移动导致新区域进入视野时，entered 包含新可见 target entities。
- [x] 5.5 当 observer 移动导致旧区域离开视野时，left 包含不再可见 target entities。
- [x] 5.6 差量计算排除 observer 自己。
- [x] 5.7 差量计算不读取 dirty tracker。
- [x] 5.8 为玩家周围 16x16 或等价 cell 视野范围保留调用入口。

## 6. Chunk/cell 存储边界
- [x] 6.1 确认 `Cell` 继续保持纯空间数据，不参与 entity 注册。
- [x] 6.2 确认 `Chunk` 对外通过 accessor 提供 cell 查询。
- [x] 6.3 避免新增依赖强制外部直接遍历 `Cell[,]`。
- [x] 6.4 记录空 chunk 延迟分配或默认 cell 存储的后续替换点。
- [x] 6.5 移除 `Cell[,]` 作为公开访问方式的要求。

## 7. 批量变更边界
- [x] 7.1 定义 cell 批量变更入口的最小数据形态。
- [x] 7.2 批量变更应支持先验证再应用。
- [x] 7.3 批量变更失败时不得留下部分 cell 状态。
- [x] 7.4 批量变更成功后统一触发 dirty 标记。
- [x] 7.5 记录后续 inverse/undo batch 的扩展点。
- [x] 7.6 不在本次实现具体建造、拆除或结构判定规则。
- [x] 7.7 不在本次把 entity add/move/remove 纳入 batch。

## 8. Shared view 后续边界
- [x] 8.1 确认 `ChunkStore` 和 `EntityTracker` 不被设计成不可拆分的单例。
- [x] 8.2 记录后续共享 chunk 数据、独立 entity tracker 的多视图扩展方向。
- [x] 8.3 不在本次实现 SharedInstance 或多世界视图。

## 9. 第一版 API 定义
- [x] 9.1 定义 `World.LoadedChunks` 的新来源与语义。
- [x] 9.2 定义 `World.RegisteredEntities` 的新来源与语义。
- [x] 9.3 定义 `World.ChangedCells` 和 `World.ChangedChunks` 的新来源与语义。
- [x] 9.4 定义 `World.AddEntity` 的新调用链。
- [x] 9.5 定义 `World.RemoveEntity` 的新调用链。
- [x] 9.6 定义 `World.MoveEntity` 的新调用链。
- [x] 9.7 定义 `World.TryGetEntity` 的新调用链。
- [x] 9.8 定义 `World.TryGetEntitiesAt` 的新调用链，默认等价于 all target。
- [x] 9.9 更新 `WorldBootstrap` 的 chunk 预热行为定义。
- [x] 9.10 更新 `MapChunkDebugWindow` 的实体索引检查行为定义。

## 10. 验证
- [x] 10.1 搜索被移动字段或方法的残留引用。
- [x] 10.2 检查 target 索引 register/move/unregister 后的一致性。
- [x] 10.3 检查 visibility delta 不依赖 dirty tracker。
- [x] 10.4 检查 dirty tracker 不承担 observer-specific 同步语义。
- [x] 10.5 运行 `dotnet build Client/DG_Client/Assembly-CSharp.csproj --no-restore`。
- [x] 10.6 运行 `dotnet build Client/DG_Client/Assembly-CSharp-Editor.csproj --no-restore`。
- [x] 10.7 运行 `openspec validate refactor-map-runtime-foundation --strict --no-interactive`。
- [x] 10.8 确认没有引入 Unity coroutine API。
- [x] 10.9 确认没有添加生成代码注释。
