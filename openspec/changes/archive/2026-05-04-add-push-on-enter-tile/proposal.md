# Change: 新增进入推动地格能力

## Request
用户希望在当前 GameCore 的 ECS-like 组件组织中实现一个“传送带”能力：当玩家或其他可移动实体进入某个地格后，该地格会按自身方向推动该实体移动一格。

本阶段仍处于 OpenSpec proposal 阶段，只补齐需求、设计、任务和 spec delta，不写运行时代码。

## Feature Description
传送带不作为核心业务类或专用实体类型实现，而是作为一种由通用组件组合出来的配置实体：

- `PositionComponent` 表示地格坐标。
- `DirectionComponent` 表示推动方向。
- `ColliderComponent` 在当前代码中表示进入空间索引、可被格子查询。
- `PushOnEnterComponent` 表示进入或停留在该格时触发推动。
- 不添加 `BlockingComponent`，因此玩家可以站到该地格上。

“传送带”这个具体种类由 Luban archetype、`ConfigId` 和 tag 表达，例如 `Tile.Conveyor`。核心规则只识别 `PushOnEnterComponent`，不识别 `ConveyorComponent` 或实体名字。

## Why
当前 GameCore 已经用组件表达实体能力，并通过 `MoveCommand` + `MovementResolveSystem` 统一裁决玩家输入和自动移动。进入推动地格应该复用这条统一移动入口，否则传送带会变成一条绕过阻挡、dirty、snapshot/delta 的特殊路径。

这个变更也为后续组件规范化提供一个样例：组件按“功能/能力”命名，而不是按“物体种类”命名。

## What Changes
- 新增通用能力组件 `PushOnEnterComponent`。
- 新增 `PushOnEnterSystem`，在服务端 tick 中扫描进入推动地格。
- `PushOnEnterSystem` 通过 `MoveCommand` 提交移动，不直接改坐标。
- Luban 增加 `PushOnEnter` 组件枚举和一个传送带 archetype。
- demo world 增加一个最小传送带实例。
- 服务端 tick 顺序明确为：自动移动、进入推动、广播 delta。
- 客户端第一版不新增协议字段，只通过 snapshot/delta 和配置标识显示传送带。

## Non-Goals
- 不重命名 `ColliderComponent`。
- 不引入 `VelocityComponent`。
- 不做链式传送带复杂连锁。
- 不做传送带动画、美术资源或完整表现表。
- 不新增传送带专用网络协议字段。
- 不把 tag 系统扩展为规则引擎。

## Success Criteria
- Luban 能配置出传送带实体，且服务端和 Unity 客户端读取同一份配置。
- 传送带实体能被空间查询找到，但不会阻挡玩家进入。
- 玩家站上传送带后，在服务端 tick 中被推动一格。
- 目标格被阻挡时，玩家不会被推动。
- 同一 tick 内同一个实体最多被推动一次。
- 推动结果进入 `WorldDelta`，两个客户端最终显示一致。

## Manual Verification
用户手动端到端验证时按以下顺序确认：

1. 启动服务端。
2. 启动两个 Unity 客户端并 JoinWorld。
3. 确认两个客户端都能看到 demo world 中的传送带实体。
4. 客户端 A 控制玩家移动到传送带格子。
5. 等待一个服务端 tick。
6. 确认客户端 A 的玩家被服务端推动到下一格。
7. 确认客户端 B 也看到同一个玩家处于同一最终坐标。
8. 将玩家移动到目标格被阻挡的传送带场景，确认玩家不会穿过阻挡体。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `authoritative-move-runner`, `client-world-runner`
- Affected code: `Shared/DG.GameCore/Components`, `Shared/DG.GameCore/Config`, `Shared/DG.GameCore/Movement`, `Server/Hotfix/AuthoritativeMove/Runtime`, `Config/Luban`, `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo/Runtime`, Unity EditMode tests
- Tests: Unity TestFramework EditMode 覆盖组件组合、进入推动、阻挡失败、单 tick 去重和客户端 snapshot 应用；手动端到端验证服务端 tick 推动后两个客户端同步一致
