## Context
当前 `Shared/DG.GameCore` 是服务端和 Unity 客户端共享的纯 C# 规则层。实体由 `GameEntity` 表达身份，组件表达能力，`GameWorld` 管理组件存储、空间索引、dirty 和 snapshot/delta。

现有移动规则已经收敛到 `MoveCommand` 和 `MovementResolveSystem`。玩家输入和自动移动都应该通过同一套裁决路径处理阻挡、占用、反弹和 dirty。传送带这类地格效果也应进入这条路径。

当前 `ColliderComponent` 的命名不够准确。它在代码中的真实作用更接近“进入空间索引、可被格子查询”，不是传统物理碰撞体。本变更不重命名它，只在文档中明确使用边界，后续组件规范化单独处理。

## Goals
- 用通用组件表达进入推动能力。
- 让传送带由 Luban 配置组合出来，而不是写成专用实体类型。
- 服务端权威 tick 驱动推动结果。
- 推动移动复用 `MovementResolveSystem`。
- 保持客户端只应用服务端 snapshot/delta，不本地裁决权威坐标。

## Non-Goals
- 不重构组件命名体系。
- 不把 `ColliderComponent` 拆成新组件。
- 不新增通用触发器框架。
- 不做复杂机关链、循环检测或多格推动。
- 不扩展网络协议。

## Decisions
### Decision: 使用 `PushOnEnterComponent`，不使用 `ConveyorComponent`
`PushOnEnterComponent` 表达通用能力：有实体进入或停留在该格时，该格可以推动该实体。传送带、风场、水流、弹簧地板等后续玩法都可以复用这个能力。

Alternatives considered: 新增 `ConveyorComponent`。拒绝，因为它把业务种类写进核心规则层，后续每新增一种类似地格都会增加专用组件和专用判断。

### Decision: 推动方向复用 `DirectionComponent`
传送带第一版只需要四方向格子推动，现有 `DirectionComponent` 已经表达方向状态，且 `MoveCommand.ToDirection` 可以直接使用。

Alternatives considered: 在 `PushOnEnterComponent` 内放方向。暂不采用，因为当前已有方向组件，复用它能减少重复数据。后续如果需要不同方向来源或多方向规则，再扩展组件数据。

### Decision: 传送带保留 `ColliderComponent`，不加 `BlockingComponent`
当前空间查询依赖实体进入空间索引。传送带需要能被系统在格子上查询到，因此使用 `ColliderComponent`。传送带不应该挡住玩家，所以不加 `BlockingComponent`。

Alternatives considered: 让 `PushOnEnterSystem` 只遍历所有传送带实体，不依赖空间索引。第一版仍保留空间索引能力，因为客户端、服务端已有格子查询模型，后续地格效果也会复用。

### Decision: 推动结果必须走 `MoveCommand`
`PushOnEnterSystem` 只负责发现“谁站在推动地格上”和生成推动命令，不直接修改坐标。最终是否能移动由 `MovementResolveSystem` 统一裁决。

Alternatives considered: `PushOnEnterSystem` 直接调用 `GameWorld.MoveEntity`。拒绝，因为这会绕过阻挡、占用、反弹和统一移动结果。

### Decision: 同一 tick 内同一实体最多被推动一次
第一版避免链式推动和循环推动。一个实体在同一 tick 内被一个传送带推动后，不再被其他传送带继续推动。下一 tick 再重新评估。

Alternatives considered: 支持链式传送带。暂不采用，因为链式规则需要顺序、循环检测和最大步数，超出最小切片。

## Component Candidates
本变更只新增 `PushOnEnterComponent`，但后续组件规范化可以围绕以下能力名整理：

- `PositionComponent`: 有格子坐标。
- `DirectionComponent`: 有方向。
- `ColliderComponent`: 当前实际语义是进入空间索引，后续可考虑改名为 `SpatialIndexComponent` 或 `CellOccupantComponent`。
- `BlockingComponent`: 阻挡其他实体进入。
- `PushOnEnterComponent`: 进入或停留时触发推动。
- `AutoMoveComponent`: 自身按 tick 自动移动。
- `BouncableComponent`: 被阻挡时可反弹。
- `PlayerControlComponent`: 玩家控制或玩家归属。

## Execution Order
服务端 tick 第一版顺序：

1. `AutoMoveSystem.Tick`
2. `PushOnEnterSystem.Tick`
3. `AuthoritativeWorldSyncSystem.BroadcastDelta`

该顺序保证自动移动先更新世界，再由地格效果处理当前位置，最后统一同步。

## Risks
- `ColliderComponent` 命名会继续造成理解成本。缓解方式：本变更文档明确它的当前语义，并把重命名留给后续规范化。
- 客户端显示层目前 snapshot 字段不包含通用组件列表。缓解方式：第一版通过 `ConfigId` 或 tag 做最小显示，不扩协议。
- 推动顺序可能影响玩法手感。缓解方式：在 spec 中固定 tick 顺序，并通过测试覆盖。

## Validation
- `openspec validate add-push-on-enter-tile --strict --no-interactive`
- Luban 导出成功。
- Unity TestFramework EditMode 覆盖组件组合、进入推动、阻挡失败、单 tick 去重和客户端 snapshot 应用。
- 用户手动端到端验证两个客户端最终坐标一致。
