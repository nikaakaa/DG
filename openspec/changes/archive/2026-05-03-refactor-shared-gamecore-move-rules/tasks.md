## 1. 范围与基线确认
- [x] 1.1 读取 `openspec/specs/authoritative-move-runner/spec.md`。
- [x] 1.2 读取 `openspec/specs/multiplayer-entity-management/spec.md`。
- [x] 1.3 读取 `openspec/specs/client-world-runner/spec.md`。
- [x] 1.4 读取 `openspec/changes/add-bouncing-ball-tick-demo/tasks.md`。
- [x] 1.5 确认 `add-bouncing-ball-tick-demo` 的旧 demo 专用验收不再作为本变更完成标准。
- [x] 1.6 记录当前两套服务端 world 分裂点：`AuthoritativeMoveWorld` 与 `BouncingDemoWorld`。
- [x] 1.7 记录当前两套同步协议分裂点：`G2C_EntityMovedNotify` 与 demo notify。

## 2. Shared GameCore 骨架
- [x] 2.1 新增 `Shared/DG.GameCore` 目录。
- [x] 2.2 建立 GameCore 编译方式，保证服务端和 Unity 客户端都能引用。
- [x] 2.3 定义 `GridCoord`。
- [x] 2.4 定义 `Direction`。
- [x] 2.5 定义 `GameEntity`。
- [x] 2.6 定义 `GameWorld`。
- [x] 2.7 定义按坐标查询实体的空间索引。
- [x] 2.8 确认 GameCore 不引用 `Fantasy`。
- [x] 2.9 确认 GameCore 不引用 `UnityEngine`。
- [x] 2.10 编译 GameCore。

## 3. 组件与配置模型
- [x] 3.1 定义 `PositionComponent`。
- [x] 3.2 定义 `DirectionComponent`。
- [x] 3.3 定义 `ColliderComponent`。
- [x] 3.4 定义 `BlockingComponent`。
- [x] 3.5 定义 `BouncableComponent`。
- [x] 3.6 定义 `AutoMoveComponent`。
- [x] 3.7 定义 `PlayerControlComponent`。
- [x] 3.8 定义 `ConfigId` 或等价配置标识。
- [x] 3.9 定义 `ArchetypeId` 或等价 archetype 标识。
- [x] 3.10 定义最小 entity 配置结构。
- [x] 3.11 通过配置创建 player entity。
- [x] 3.12 通过配置创建 ball entity。
- [x] 3.13 通过配置创建 blocking entity。
- [x] 3.14 确认规则查询不依赖 entity 名字。

## 4. MoveCommand 与统一移动裁决
- [x] 4.1 定义 `MoveCommand`。
- [x] 4.2 定义 `MoveCommandSource`，至少区分玩家输入和自动 tick。
- [x] 4.3 定义 `MoveResult`。
- [x] 4.4 定义 `CollisionInfo` 或等价碰撞结果。
- [x] 4.5 实现 `MovementResolveSystem` 普通移动。
- [x] 4.6 实现目标格 `BlockingComponent` 拒绝进入。
- [x] 4.7 实现目标格玩家占位拒绝进入。
- [x] 4.8 实现有 `BouncableComponent` 的实体碰撞后反向 `DirectionComponent`。
- [x] 4.9 确认反弹时实体不进入被阻挡目标格。
- [x] 4.10 确认移动成功和反弹失败路径都标记 dirty。

## 5. 自动 Tick 移动
- [x] 5.1 实现 `AutoMoveSystem`。
- [x] 5.2 `AutoMoveSystem` 查询 `PositionComponent + DirectionComponent + AutoMoveComponent` 实体。
- [x] 5.3 `AutoMoveSystem` 按 tick 间隔产生 `MoveCommand`。
- [x] 5.4 自动移动命令进入 `MovementResolveSystem`。
- [x] 5.5 确认自动移动不直接改写坐标。
- [x] 5.6 确认第一版不引入 Velocity 概念。

## 6. 服务端权威 World 迁移
- [x] 6.1 让服务端权威移动逻辑持有一个 GameCore `GameWorld`。
- [x] 6.2 JoinWorld 时在同一个 GameWorld 创建 player entity。
- [x] 6.3 玩家移动 RPC 转换为 `MoveCommand`。
- [x] 6.4 反弹球配置在同一个 GameWorld 创建 ball entity。
- [x] 6.5 阻挡体或边界体在同一个 GameWorld 创建 blocking entity。
- [x] 6.6 服务端 tick runner 调用 `AutoMoveSystem`。
- [x] 6.7 移除跨 world 查询的反弹逻辑。
- [x] 6.8 确认球碰玩家反弹来自同一 GameWorld 的空间查询。

## 7. 协议与生成
- [x] 7.1 设计统一 `G2C_WorldSnapshotNotify` 和 `G2C_WorldDeltaNotify` 协议字段。
- [x] 7.2 更新 `Tools/NetworkProtocol/Outer/OuterMessage.proto`。
- [x] 7.3 运行协议导出工具。
- [x] 7.4 检查服务端生成目录包含新增 snapshot/delta 消息和 opcode。
- [x] 7.5 检查客户端生成目录包含新增 snapshot/delta 消息、opcode 和 helper。
- [x] 7.6 确认没有手动修改生成目录文件。
- [x] 7.7 移除新链路对旧 `G2C_EntityMovedNotify` 与 demo notify 的依赖。

## 8. 服务端同步
- [x] 8.1 定义 `DirtyChange`。
- [x] 8.2 定义 `EntitySnapshot`。
- [x] 8.3 定义 `WorldDelta`。
- [x] 8.4 服务端玩家移动成功后产生统一 delta。
- [x] 8.5 服务端球自动移动成功后产生统一 delta。
- [x] 8.6 服务端球碰玩家反弹后产生 direction 或状态 delta。
- [x] 8.7 服务端 JoinWorld 后发送当前 world snapshot。
- [x] 8.8 服务端 observer 为空时不阻塞 tick。
- [x] 8.9 服务端广播日志记录 server tick、entity id、组件状态和 observer 数量。

## 9. Unity 客户端接收与显示
- [x] 9.1 客户端接入 GameCore 数据结构或等价镜像结构。
- [x] 9.2 客户端收到 world snapshot 后创建或更新玩家。
- [x] 9.3 客户端收到 world snapshot 后创建或更新球。
- [x] 9.4 客户端收到 world snapshot 后创建或更新阻挡体。
- [x] 9.5 客户端收到 world delta 后应用玩家移动。
- [x] 9.6 客户端收到 world delta 后应用球移动或反弹方向。
- [x] 9.7 客户端显示层按组件/tag 区分玩家、球和阻挡体。
- [x] 9.8 客户端不本地推进球的权威坐标。
- [x] 9.9 保留 `ClientWorldRunnerDemo` 场景作为手动验收入口。
- [x] 9.10 缺失当前 client world 时输出可验证日志。

## 10. 自动化测试
- [x] 10.1 新增 GameCore 测试：普通移动成功。
- [x] 10.2 新增 GameCore 测试：目标格阻挡拒绝进入。
- [x] 10.3 新增 GameCore 测试：玩家占位拒绝进入。
- [x] 10.4 新增 GameCore 测试：球碰阻挡体反弹。
- [x] 10.5 新增 GameCore 测试：球碰玩家反弹。
- [x] 10.6 新增 GameCore 测试：AutoMove 产生 MoveCommand。
- [x] 10.7 新增服务端验证：JoinWorld 创建玩家进入统一 GameWorld。
- [x] 10.8 新增服务端验证：玩家移动和球自动移动都产生统一 dirty/delta。
- [x] 10.9 新增 Unity TestFramework 测试：snapshot 创建玩家、球和阻挡体。
- [x] 10.10 新增 Unity TestFramework 测试：delta 更新球反弹后的状态。

## 11. 构建与验证
- [x] 11.1 运行 GameCore 编译或测试命令。
- [x] 11.2 运行 `dotnet build Server\Server.sln -v minimal`。
- [x] 11.3 运行服务端验证项目。
- [x] 11.4 运行 `dotnet build .\Client\DG_Client\Assembly-CSharp.csproj --no-restore -v minimal`。
- [x] 11.5 运行 `dotnet build .\Client\DG_Client\Assembly-CSharp-Editor.csproj --no-restore -v minimal`。
- [x] 11.6 运行 Unity TestFramework EditMode 测试。
- [x] 11.7 运行 `openspec validate refactor-shared-gamecore-move-rules --strict --no-interactive`。

## 12. 手动端到端验收
- [x] 12.1 启动服务端。
- [x] 12.2 启动 Unity 客户端 A。
- [x] 12.3 客户端 A JoinWorld 成功并显示本地玩家。
- [x] 12.4 服务端启动同一 GameWorld 内的自动移动球。
- [x] 12.5 客户端 A 显示球和阻挡体。
- [x] 12.6 移动玩家到球路径前方。
- [x] 12.7 确认服务端日志显示球碰玩家反弹。
- [x] 12.8 确认客户端 A 显示球反弹后的服务端状态。
- [x] 12.9 启动 Unity 客户端 B。
- [x] 12.10 确认 A 和 B 都显示同一玩家、球、阻挡体状态。
- [x] 12.11 确认 A 和 B 同一 server tick 的球坐标和方向一致。
- [x] 12.12 停止服务端，确认客户端不再生成新的权威球坐标。

## 13. 文档与收口
- [x] 13.1 记录 Shared GameCore 的实际文件路径。
- [x] 13.2 记录服务端 Handler/System/Sync 的实际文件路径。
- [x] 13.3 记录客户端接收/应用/显示的实际文件路径。
- [x] 13.4 记录自动化测试命令和结果。
- [x] 13.5 记录手动端到端验收步骤和观察结果。
- [x] 13.6 记录 `add-bouncing-ball-tick-demo` 被本变更替代的收口方式。
- [x] 13.7 所有任务完成后再勾选本文件所有任务。

