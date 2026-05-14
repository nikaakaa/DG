## 1. Proposal Validation

- [ ] 1.1 复核 `openspec list`，确认本变更与现有 active changes 的依赖关系。
- [ ] 1.2 复核 `shared-gamecore-entity-rules`、`authoritative-move-runner`、`client-world-runner` 当前 spec。
- [ ] 1.3 复核 `docs/Goal/ecs-storage-and-runtime-evolution.md` 和 `docs/Goal/world-storage-next-step.md`。
- [ ] 1.4 复核 `Ref/Arch` 的 target framework、依赖和 license。
- [ ] 1.5 运行 `openspec validate refactor-server-world-storage-to-arch-backend --strict --no-interactive`。

## 2. Dependency Boundary

- [ ] 2.1 确认 `Shared/DG.GameCore` 直接引用 Arch 是否同时满足 Server 和 Unity 编译。
- [ ] 2.2 如果 Unity 编译链不接受 Arch 依赖，新增服务端专用 Arch storage 项目方案。
- [ ] 2.3 确认 Arch 依赖不会引入 UnityEngine、Fantasy 或协议生成类型到 Shared 规则核心。
- [ ] 2.4 确认 client mirror 不需要引用 Arch 权威 API。

## 3. Storage Backend

- [ ] 3.1 新增 `ArchWorldDataStorage`，实现 `IWorldDataStorage`。
- [ ] 3.2 建立 `EntityId -> Arch.Entity` 映射。
- [ ] 3.3 建立 `Arch.Entity -> EntityId` 映射。
- [ ] 3.4 实现 `AddEntity`。
- [ ] 3.5 实现 `RemoveEntity`。
- [ ] 3.6 实现 `TryGetEntity`。
- [ ] 3.7 实现 `EnumerateEntities`。
- [ ] 3.8 实现 `SetComponent<TComponent>`。
- [ ] 3.9 实现 `RemoveComponent<TComponent>`。
- [ ] 3.10 实现 `HasComponent<TComponent>`。
- [ ] 3.11 实现 `TryGetComponent<TComponent>`。
- [ ] 3.12 实现 `GetComponent<TComponent>`。
- [ ] 3.13 实现 `QueryEntities`。

## 4. Hot Path Query

- [ ] 4.1 覆盖 `Position + Direction + AutoMove` query。
- [ ] 4.2 覆盖 `Position + Direction + PushOnEnter` query。
- [ ] 4.3 覆盖 `Position + Collider` query。
- [ ] 4.4 覆盖 `Position + Blocking` query。
- [ ] 4.5 覆盖 `Position + Pushable` query。
- [ ] 4.6 覆盖 `Position + PortConnector` query。
- [ ] 4.7 覆盖 `MovementPermissionComponent` 读取。
- [ ] 4.8 覆盖 `TagSetComponent` 读取。
- [ ] 4.9 确认 query 返回顺序在需要确定性时使用 `EntityIterationOrder.EntityId`。
- [ ] 4.10 确认规则层调用方不接触 Arch query 类型。

## 5. GameWorld Integration

- [ ] 5.1 增加服务端创建 Arch-backed `GameWorld` 的入口。
- [ ] 5.2 保留 indexed storage 入口用于对照测试和 fallback。
- [ ] 5.3 确认 `SetComponent` 后 spatial 同步仍由 `GameWorld` 外层执行。
- [ ] 5.4 确认 `SetComponent` 后 dirty journal 仍由 `GameWorld` 外层执行。
- [ ] 5.5 确认 runtime effect final component merge 不绕过 `GameWorld` component API。
- [ ] 5.6 确认 snapshot/delta 不读取 Arch 内部 id。
- [ ] 5.7 确认 animation metadata 不读取 Arch 内部 id。

## 6. Fantasy Server Wiring

- [ ] 6.1 修改 `AuthoritativeMoveWorldProvider`，服务端默认创建 Arch-backed `GameWorld`。
- [ ] 6.2 确认 `AuthoritativeWorldTickRunner` 不直接引用 Arch。
- [ ] 6.3 确认 C2G Handler 不直接引用 Arch。
- [ ] 6.4 确认 observer/session ownership 不存 Arch entity。
- [ ] 6.5 确认 JoinWorld snapshot 来自 DG `GameWorld`。
- [ ] 6.6 确认 WorldDelta broadcast 来自 DG `GameWorld`。

## 7. Unified Speed Model

- [ ] 7.1 明确服务端 tick interval 是唯一权威结算节拍。
- [ ] 7.2 明确玩家移动使用 action cost / ready tick。
- [ ] 7.3 明确自动移动使用 ActionSpec 或配置化 cost policy。
- [ ] 7.4 明确机关推动使用 ActionSpec 或配置化 cost policy。
- [ ] 7.5 明确 connected body movement 成功提交后每个成员共享同一 server tick。
- [ ] 7.6 明确客户端表现速度只消费 server tick、delta metadata 和表现配置。
- [ ] 7.7 覆盖客户端表现追帧时最终落点必须等于服务端 delta。

## 8. Shared Automated Tests

- [ ] 8.1 Unity TestFramework EditMode：Arch backend Add/Get/Remove entity 等价 indexed backend。
- [ ] 8.2 Unity TestFramework EditMode：Arch backend Set/TryGet/Has/Remove component 等价 indexed backend。
- [ ] 8.3 Unity TestFramework EditMode：删除 entity 后旧 `EntityId` 不会误读新 Arch entity。
- [ ] 8.4 Unity TestFramework EditMode：`EntityIterationOrder.EntityId` 顺序稳定。
- [ ] 8.5 Unity TestFramework EditMode：auto move query 结果等价。
- [ ] 8.6 Unity TestFramework EditMode：push-on-enter query 结果等价。
- [ ] 8.7 Unity TestFramework EditMode：blocking/collider/pushable 空间查询语义不变。
- [ ] 8.8 Unity TestFramework EditMode：dirty/delta 不暴露 Arch id。
- [ ] 8.9 Unity TestFramework EditMode：runtime effect final component 经过 Arch backend 后结果一致。
- [ ] 8.10 Unity TestFramework EditMode：client mirror 只应用服务端 snapshot/delta。

## 9. Server Verification

- [ ] 9.1 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [ ] 9.2 `dotnet build Server/Server.sln -v minimal`。
- [ ] 9.3 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [ ] 9.4 Server verification 覆盖 JoinWorld。
- [ ] 9.5 Server verification 覆盖合法玩家移动。
- [ ] 9.6 Server verification 覆盖阻挡失败。
- [ ] 9.7 Server verification 覆盖自动移动。
- [ ] 9.8 Server verification 覆盖机关推动。
- [ ] 9.9 Server verification 覆盖 connected body。
- [ ] 9.10 Server verification 覆盖 debug spawn/move/remove。
- [ ] 9.11 Server verification 覆盖 runtime effect。
- [ ] 9.12 Server verification 覆盖同一 tick 的 WorldDelta server tick 和 animation metadata。

## 10. Manual End-to-End Verification

> 由用户手动执行，不要求 AI 代替完成。

- [ ] 10.1 启动 Fantasy 服务端。
- [ ] 10.2 Unity Play Mode 启动第一个客户端并 JoinWorld。
- [ ] 10.3 启动第二个客户端并 JoinWorld / observer。
- [ ] 10.4 客户端 A 执行玩家移动，确认 A/B 最终坐标一致。
- [ ] 10.5 验证阻挡失败不会污染 B。
- [ ] 10.6 验证自动移动按服务端 tick 同步。
- [ ] 10.7 验证机关推动按服务端 tick 同步。
- [ ] 10.8 验证 connected body 成员同 tick 同步。
- [ ] 10.9 验证 debug spawn/move/remove 同步。
- [ ] 10.10 验证 runtime effect 最终组件同步。
- [ ] 10.11 确认客户端没有本地裁决最终坐标。
- [ ] 10.12 确认客户端表现速度只跟随服务端 delta 和 metadata。
