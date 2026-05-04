## 1. 范围确认
- [x] 1.1 确认 `PushOnEnterComponent` 只表示通用进入推动能力，不使用 `ConveyorComponent` 作为核心组件名。
- [x] 1.2 确认本变更不重命名 `ColliderComponent`，后续组件规范化单独处理。
- [x] 1.3 确认传送带第一版只推动拥有 `PositionComponent` 的实体。
- [x] 1.4 确认传送带第一版不推动自身。

## 2. Luban 配置
- [x] 2.1 在 Luban `ComponentKind` 增加 `PushOnEnter`。
- [x] 2.2 在运行时 `ComponentKind` 增加同值 `PushOnEnter`。
- [x] 2.3 在 `entity_archetype.xlsx` 增加传送带 archetype，组合为 `Position`、`Direction`、`Collider`、`PushOnEnter`。
- [x] 2.4 给传送带 archetype 增加 tag，例如 `Tile.Conveyor`。
- [x] 2.5 在 `world_spawn.xlsx` 的 demo world 增加至少一个传送带实例。
- [x] 2.6 运行 Luban 导出，确认共享 C# 表代码、服务端 JSON 和 Unity `StreamingAssets/GameConfig` 同步更新。

## 3. GameCore 组件和构建
- [x] 3.1 新增 `PushOnEnterComponent`，保持为纯数据或空标记组件。
- [x] 3.2 在 `EntityBuilder` 中添加 `ComponentKind.PushOnEnter` 到 `PushOnEnterComponent` 的应用逻辑。
- [x] 3.3 确认传送带实体拥有 `ColliderComponent` 后能进入空间查询。
- [x] 3.4 确认传送带实体不拥有 `BlockingComponent` 时不会阻挡玩家进入。

## 4. 进入推动规则
- [x] 4.1 新增 `PushOnEnterSystem`，扫描拥有 `PushOnEnterComponent`、`PositionComponent`、`DirectionComponent` 的地格实体。
- [x] 4.2 对地格所在坐标的其他实体生成 `MoveCommand`。
- [x] 4.3 将推动移动交给 `MovementResolveSystem` 裁决。
- [x] 4.4 被推动目标格被阻挡时保持被推动实体原坐标。
- [x] 4.5 同一服务端 tick 内同一个实体最多被推动一次。
- [x] 4.6 明确执行顺序：`AutoMoveSystem` 之后执行 `PushOnEnterSystem`，再广播 world delta。

## 5. 服务端接入
- [x] 5.1 在 `AuthoritativeMoveWorldProvider` 中创建 `PushOnEnterSystem`。
- [x] 5.2 在 `AuthoritativeWorldTickRunner` 中接入推动系统。
- [x] 5.3 确认推动产生的坐标变化进入 `WorldDelta`。

## 6. 客户端表现
- [x] 6.1 客户端 snapshot/delta 能创建或更新传送带 mirror entity。
- [x] 6.2 最小显示层能根据 `ConfigId` 或 tag 区分传送带。
- [x] 6.3 第一版不新增传送带专用网络协议字段。

## 7. Unity TestFramework 测试
- [x] 7.1 添加 EditMode 测试：Luban 传送带 archetype 生成 `Position`、`Direction`、`Collider`、`PushOnEnter`。
- [x] 7.2 添加 EditMode 测试：传送带不阻挡玩家进入。
- [x] 7.3 添加 EditMode 测试：玩家站在向右传送带上，tick 后移动到右侧一格。
- [x] 7.4 添加 EditMode 测试：目标格有阻挡体时玩家不会被推动。
- [x] 7.5 添加 EditMode 测试：同一 tick 内一个实体只被推动一次。
- [x] 7.6 添加 EditMode 测试：客户端 apply snapshot 后能保留传送带 mirror entity。

## 8. 验证
- [x] 8.1 运行 `openspec validate add-push-on-enter-tile --strict --no-interactive`。
- [x] 8.2 运行 Luban 导出命令。
- [x] 8.3 运行 Unity TestFramework EditMode。
- [ ] 8.4 手动端到端验证：启动服务端和两个客户端，玩家进入传送带后被服务端推动，两个客户端最终坐标一致。
