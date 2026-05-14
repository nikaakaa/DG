## 1. Proposal Validation

- [x] 1.1 复核 active changes，确认本变更不与 `enforce-data-driven-behavior-policy` 的 apply 范围冲突。
- [x] 1.2 运行 `openspec validate refactor-gamecore-ecs-storage-and-id-policy --strict --no-interactive`。

## 2. Runtime ID Boundary

- [x] 2.1 梳理所有运行时 spec/policy/tag 字符串入口：Luban action spec、blocked policy、push-on-enter output、deferred output、animation style、sandbox/test authoring。
- [x] 2.2 定义 `ActionSpecId`、`BlockedResultPolicyId`、表现 style id、tag 的运行时 ID 合同。
- [x] 2.3 建立配置 alias/name 到运行时 ID 的解析边界。
- [x] 2.4 建立允许清单：配置导入、错误信息、日志、测试 authoring、表现层可以保留字符串。
- [x] 2.5 建立禁止清单：rules/arbitration/planning/commit/pending/deferred 不得通过字符串选择策略。

## 3. ECS Storage Boundary

- [x] 3.1 梳理 `GameWorld` 对外 API，标注哪些调用方依赖排序、拷贝数组、字典语义或全表枚举。
- [x] 3.2 定义 entity location façade，不暴露未来内部是字典、数组还是 archetype chunk。
- [x] 3.3 定义 component query / subset index API，用于替代热路径全表扫。
- [x] 3.4 定义空间索引与 entity location 的对接边界，保持 map chunk/cell 与 ECS archetype chunk 分离。
- [x] 3.5 定义 dirty/snapshot/delta 在存储替换前后的语义不变条件。

## 4. Observability

- [x] 4.1 为 `EnumerateEntities`、`TryGetComponent`、`HasComponent`、空间查询加入可开关计数或测试可见统计。
- [x] 4.2 记录自动移动入队、push 传播、connected body 查询、snapshot/delta flush 的调用次数和分配热点。
- [x] 4.3 提供测试或诊断入口，能证明优化前后的语义一致且观测数据可读。

## 5. Phase 0 Implementation

- [x] 5.1 将规则热路径中普通行为 spec/policy 字符串比较替换为运行时 ID 或显式 policy 字段。
- [x] 5.2 将底层行为 tag 判断统一为 `WorldTag` / `TagSetComponent`，禁止规则层读取字符串 tag 集合。
- [x] 5.3 为 AutoMove、Position+Collider、PushOnEnter、PortConnector 预留 subset/query 边界。
- [x] 5.4 拆分无序枚举和有序枚举，避免调用方默认拿排序数组。
- [x] 5.5 减少空间查询中不必要的数组拷贝，保持只读访问语义。

## 6. Unity TestFramework

- [x] 6.1 EditMode：两个不同配置 alias 映射到相同运行时 policy 数据时行为等价。
- [x] 6.2 EditMode：修改显式 policy 字段会改变行为，不需要字符串分支。
- [x] 6.3 EditMode：规则层扫描确认无新增普通行为字符串 spec/policy/tag 策略分支。
- [x] 6.4 EditMode：`GameWorld` 查询 API 在存储边界收口前后返回语义一致。
- [x] 6.5 EditMode：dirty、snapshot、WorldDelta 在同一世界状态下保持字段一致。

## 7. Server Verification

- [x] 7.1 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 7.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 7.3 确认服务端权威路径不通过字符串 action 名字决定 arbitration、handoff、pending、planning 或 commit 策略。

## 8. Manual Verification

> 由用户手动执行，不要求 AI 代替完成。

- [ ] 8.1 在 Unity Play Mode 中连接服务端权威路径。
- [ ] 8.2 启动两个客户端并注册 observer。
- [ ] 8.3 触发玩家移动、自动移动、机关推动、configured wind push 或 handoff 行为。
- [ ] 8.4 确认两个客户端收到同一份服务端最终 `WorldDelta`。
- [ ] 8.5 确认客户端没有本地通过字符串 spec/tag 决定权威策略或最终坐标。
