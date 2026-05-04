## 1. Minestom Reference Boundary
- [x] 1.1 对照 `Ref/Minestom/src/main/java/net/minestom/server/thread/TickSchedulerThread.java`，确认只借固定 tick 思想，不创建独立 tick thread。
- [x] 1.2 对照 `Ref/Minestom/src/main/java/net/minestom/server/ServerProcessImpl.java`，确认客户端只实现 `Update accumulator -> ClientWorldRunner -> systems -> World` 的最小链路。
- [x] 1.3 对照 `Ref/Minestom/src/main/java/net/minestom/server/instance/Instance.java`，确认 `World + ClientWorldContext` 承担客户端 world/instance 归属边界。
- [x] 1.4 对照 `Ref/Minestom/src/main/java/net/minestom/server/instance/EntityTracker.java`，确认继续使用当前 `EntityTarget.All / Player / Monster / Object`，不迁移 Java Target 类型系统。
- [x] 1.5 对照 `Ref/Minestom/src/main/java/net/minestom/server/instance/batch/Batch.java`，确认本阶段只保留 dirty flush 和 batch 边界，不实现异步批处理或 inverse batch。
- [x] 1.6 明确排除 `ThreadDispatcher`、`Acquirable`、server scheduler、connection tick、packet flush、Minecraft 物理 tick、effect tick 和服务端事件总线。

## 2. Runtime Boundaries
- [x] 2.1 创建客户端世界推进器相关类型的 runtime 目录。
- [x] 2.2 新增客户端系统接口，tick 方法接收运行时上下文和 delta time。
- [x] 2.3 新增运行时上下文，暴露 `World`、tick 序号、tick 时间、fixed delta time 和命令缓冲。
- [x] 2.4 增加构造保护，确保没有有效 `World` 时不能创建运行时上下文。
- [x] 2.5 确保运行时上下文只包含 world、tick、时间和已知命令缓冲，不引入开放式杂项容器。

## 3. Command Buffer
- [x] 3.1 新增移动命令类型，记录 entity id、目标坐标和 entity target。
- [x] 3.2 新增移动命令缓冲，支持 enqueue、count、consume 和 clear 行为。
- [x] 3.3 确保已消费命令不会在后续 tick 被重复执行。
- [x] 3.4 确保外部输入通过命令缓冲提交移动意图，而不是绕过推进器直接调用 `World.MoveEntity`。

## 4. Systems
- [x] 4.1 新增 movement system，读取移动命令并调用 `World.MoveEntity`。
- [x] 4.2 确保缺失 entity 的移动命令会被忽略且不抛异常。
- [x] 4.3 新增 dirty flush system，在移动后观察 changed cells 和 changed chunks。
- [x] 4.4 确保 dirty flush 只在捕获 dirty 数据之后清理 dirty state。
- [x] 4.5 保持系统为普通 C# 对象，不依赖场景对象、不继承 `MonoBehaviour`。

## 5. Runner
- [x] 5.1 新增依赖 `WorldBootstrap` 的 `ClientWorldRunner` MonoBehaviour。
- [x] 5.2 新增可序列化 fixed tick interval，并设置保守默认值。
- [x] 5.3 串联默认系统顺序：movement 先执行，dirty flush 后执行。
- [x] 5.4 在 `Update` 中通过 accumulator 推进逻辑。
- [x] 5.5 增加每帧最大 tick 限制或对应测试，避免卡顿后一帧无限追 tick。
- [x] 5.6 暴露最小命令提交 API，供本地测试或 debug input 使用。
- [x] 5.7 确认 runner 不引入独立线程、不接入网络 session、不处理 packet flush。

## 6. Validation
- [x] 6.1 新增 EditMode 测试，覆盖移动命令消费。
- [x] 6.2 新增 EditMode 测试，证明一次 tick 能通过 `World` 移动 entity。
- [x] 6.3 新增 EditMode 测试，证明 dirty cells/chunks 会先被捕获再被清理。
- [x] 6.4 新增 EditMode 测试，证明 no-op tick 不会修改 world state。
- [x] 6.5 新增 EditMode 测试或结构检查，证明 `ClientWorldRunner` 使用主线程 accumulator，而不是独立 tick thread。
- [x] 6.6 新增 EditMode 测试或结构检查，证明默认系统顺序符合 Minestom tick 阶段借鉴：命令/逻辑先执行，dirty flush 后执行。
- [x] 6.7 运行 Unity EditMode tests，覆盖地图运行时测试集。
- [x] 6.8 归档前运行 OpenSpec validation。
