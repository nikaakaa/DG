## ADDED Requirements
### Requirement: 客户端世界推进器
客户端 SHALL 提供一个运行时推进器，通过固定逻辑 tick 推进客户端地图逻辑。

#### Scenario: 推进一个固定 tick
- **WHEN** 累积的 Unity 帧时间达到配置的 fixed tick interval
- **THEN** 推进器推进一次客户端地图逻辑
- **AND** 运行时上下文的 tick index 增加一

#### Scenario: 未达到间隔时不推进
- **WHEN** 累积的 Unity 帧时间低于配置的 fixed tick interval
- **THEN** 不 tick 任何客户端系统
- **AND** 运行时上下文的 tick index 不变化

#### Scenario: 推进器使用 world bootstrap
- **WHEN** 推进器在场景中初始化
- **THEN** 它从 `WorldBootstrap` 获取客户端 `World`
- **AND** 当 bootstrap world 可用时，不创建第二个独立 `World`

### Requirement: 客户端运行时上下文
客户端 SHALL 提供运行时上下文，用来承载地图 world、tick 时间、tick 序号以及客户端系统使用的命令缓冲。

#### Scenario: 上下文需要 world
- **WHEN** 代码创建运行时上下文
- **THEN** 必须提供有效的 `World`

#### Scenario: 上下文暴露 tick 状态
- **WHEN** 推进器推进一个 fixed tick
- **THEN** 运行时上下文暴露当前 tick index
- **AND** 运行时上下文向系统暴露 fixed delta time

#### Scenario: 上下文暴露命令缓冲
- **WHEN** 系统 tick
- **THEN** 系统从运行时上下文中的命令缓冲读取客户端意图

### Requirement: 客户端系统调度
客户端 SHALL 通过客户端系统调度地图运行时逻辑，而不是让分散代码直接修改 world。

#### Scenario: 系统收到共享上下文
- **WHEN** 推进器推进一个 tick
- **THEN** 每个已注册客户端系统收到同一个运行时上下文

#### Scenario: 系统按确定顺序运行
- **WHEN** 注册了多个客户端系统
- **THEN** 它们按推进器定义的顺序被 tick

#### Scenario: 默认系统顺序
- **WHEN** 使用默认 runner 配置
- **THEN** 先处理移动命令
- **AND** 再 flush dirty state

### Requirement: 移动命令缓冲
客户端 SHALL 在把移动应用到 `World` 之前，先把移动意图收集到命令缓冲中。

#### Scenario: 入队移动命令
- **WHEN** 外部代码请求移动某个 entity
- **THEN** 一个移动命令被加入 movement command buffer

#### Scenario: 消费移动命令
- **WHEN** movement system tick
- **THEN** 它消费已排队的移动命令
- **AND** 已消费命令不会在后续 tick 再次应用

#### Scenario: 缺失 entity 的命令
- **WHEN** 移动命令目标 entity id 在 `World` 中不存在
- **THEN** movement system 忽略该命令
- **AND** 当前 tick 继续执行且不抛异常

### Requirement: 移动系统
客户端 SHALL 提供 movement system，通过 `World.MoveEntity` 应用已排队的移动命令。

#### Scenario: 移动已注册 entity
- **WHEN** 移动命令指向一个已注册 entity
- **THEN** movement system 通过 `World.MoveEntity` 将该 entity 移动到命令目标坐标

#### Scenario: 移动更新 dirty state
- **WHEN** 移动成功
- **THEN** 旧 cell 和新 cell 可从 `World.ChangedCells` 读取
- **AND** 受影响 chunk 可从 `World.ChangedChunks` 读取

#### Scenario: 移动保留 entity target
- **WHEN** 移动命令包含 entity target
- **THEN** movement system 将该 target 传给 `World.MoveEntity`

### Requirement: Dirty Flush 边界
客户端 SHALL 提供 dirty flush 边界，在逻辑系统运行后捕获 changed cells 和 changed chunks。

#### Scenario: 清理前捕获 dirty state
- **WHEN** dirty flush 在成功移动后运行
- **THEN** changed cells 和 changed chunks 被捕获给下游消费者
- **AND** dirty state 只在捕获之后被清理

#### Scenario: 空 dirty flush
- **WHEN** 当前 tick 没有 world 变化
- **THEN** dirty flush 产生空结果
- **AND** dirty state 保持为空

#### Scenario: 延后渲染实现
- **WHEN** dirty flush 捕获 changed cells 或 chunks
- **THEN** 本变更不要求实现地图渲染刷新
- **AND** 下游渲染可以由后续变更添加

### Requirement: 本地最小切片
第一版客户端世界推进器 SHALL 保持本地单机逻辑，并且 SHALL NOT 实现网络、预测、回滚、插值、寻路、战斗或渲染。

#### Scenario: 无网络依赖
- **WHEN** 推进器推进本地 tick
- **THEN** 它不需要有效的网络 session

#### Scenario: 无预测系统
- **WHEN** movement system 应用命令
- **THEN** 它不添加预测、回滚或校正状态

#### Scenario: 无渲染实现
- **WHEN** dirty state 被 flush
- **THEN** 系统不实例化、移动或销毁 Unity 视觉对象

### Requirement: Minestom 参考边界
客户端世界推进器 SHALL 参考 `Ref/Minestom` 的固定 tick、world/instance 分层、target 索引、视野差量和 batch 边界思想，并且 SHALL NOT 直接移植 Minestom 的 Java 服务端线程、网络或完整实体模拟。

#### Scenario: 借鉴固定 tick 形状
- **WHEN** 实现客户端推进器
- **THEN** 它参考 Minestom 的固定 tick 推进思想
- **AND** 它通过 Unity `Update` accumulator 在主线程推进客户端逻辑

#### Scenario: 借鉴 world 归属边界
- **WHEN** 客户端系统需要修改地图状态
- **THEN** 修改通过 `ClientWorldRunner` 调度的系统进入 `World`
- **AND** 外部输入不直接绕过推进器修改 `World`

#### Scenario: 不移植服务端复杂度
- **WHEN** 实现第一版客户端推进器
- **THEN** 不引入 `ThreadDispatcher`、`Acquirable`、server scheduler、connection tick 或 packet flush
- **AND** 不引入 Minecraft 物理 tick、effect tick 或服务端事件总线
