# client-world-runner Specification

## Purpose
TBD - created by archiving change add-client-world-runner. Update Purpose after archive.
## Requirements
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
客户端世界推进器 SHALL 保留本地固定 tick、命令缓冲和 dirty flush 边界；在服务端权威移动接入后，玩家移动输入 SHALL 能切换为网络请求驱动，且只有服务端成功响应才应用到本地 world。

#### Scenario: 无服务端响应不更新玩家坐标
- **WHEN** 玩家输入移动方向
- **AND** 客户端处于服务端权威移动模式
- **AND** 尚未收到 `G2C_MoveResponse`
- **THEN** 客户端不直接通过本地移动规则更新玩家坐标

#### Scenario: 成功响应应用最终坐标
- **WHEN** 客户端收到 `G2C_MoveResponse`
- **AND** `Success` 为 true
- **THEN** 客户端将玩家坐标更新为响应中的 `FinalX` 和 `FinalY`
- **AND** dirty flush 后可视化能反映该最终坐标

#### Scenario: 失败响应保持原坐标
- **WHEN** 客户端收到 `G2C_MoveResponse`
- **AND** `Success` 为 false
- **THEN** 客户端保持玩家移动前坐标
- **AND** 客户端记录 `MoveErrorCode` 和 `Reason` 供验证

#### Scenario: 本地 runner 形状保留
- **WHEN** 客户端接入服务端权威移动
- **THEN** `ClientWorldRunner` 仍负责固定 tick 推进和系统调度
- **AND** 移动结果应用仍进入客户端 world 与 dirty flush 链路

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

### Requirement: 客户端 observer 注册
客户端 SHALL 在双客户端同步验证前通过最小注册请求进入服务端 observer 集合，而不是依赖先发送移动请求才被服务端发现。

#### Scenario: 连接后注册 observer
- **WHEN** 客户端 Fantasy session 可用
- **AND** demo entity id 已确定
- **THEN** 客户端发送 observer 注册请求
- **AND** 注册成功后客户端可以接收其他客户端触发的 `G2C_EntityMovedNotify`

#### Scenario: B 未移动也能接收广播
- **WHEN** 客户端 B 已完成 observer 注册
- **AND** B 尚未发送 `C2G_MoveRequest`
- **THEN** 客户端 B 仍能接收 A 成功移动产生的 `G2C_EntityMovedNotify`

#### Scenario: 注册不创建第二个 world
- **WHEN** 客户端处理 observer 注册响应
- **THEN** 客户端复用当前 `ClientWorldRunner` 和 `World`
- **AND** 客户端不创建第二个独立 `World`

### Requirement: 客户端移动通知接收
客户端 SHALL 通过 Fantasy push handler 接收服务端统一 WorldSnapshot/WorldDelta，并把服务端最终状态应用到当前客户端 world。客户端 MUST NOT 通过本地规则推导玩家、远端玩家或自动移动球的权威坐标。

#### Scenario: 接收移动通知更新本地 world
- **WHEN** 客户端收到服务端同步的 entity 最终状态
- **AND** 通知中的 entity id 在当前 `World` 中存在
- **THEN** 客户端将该 entity 更新到服务端最终坐标
- **AND** 更新进入现有 dirty flush 链路

#### Scenario: 缺失 entity 的通知不打断客户端
- **WHEN** 客户端收到服务端同步的 entity 最终状态
- **AND** 通知中的 entity id 在当前 `World` 中不存在
- **THEN** 客户端可以根据 snapshot/delta 创建镜像 entity 或记录可验证日志
- **AND** 客户端不抛异常

#### Scenario: 重复最终坐标保持幂等
- **WHEN** 客户端已位于同步消息的最终坐标
- **AND** 再次收到相同 entity id 和最终坐标的同步消息
- **THEN** 客户端坐标保持不变
- **AND** 不产生错误的额外移动结果

#### Scenario: 自动移动球不由客户端推进
- **WHEN** 客户端尚未收到下一次服务端 world delta
- **AND** 本地镜像 entity 拥有自动移动或方向状态
- **THEN** 客户端不使用本地规则推进该 entity 的权威坐标
- **AND** 该 entity 的下一次权威位置变化来自服务端同步

### Requirement: 双客户端移动同步
客户端 SHALL 支持两个已连接客户端通过服务端广播同步同一 entity 的权威移动结果。

#### Scenario: A 移动后 B 应用服务端通知
- **WHEN** 客户端 A 发起合法移动
- **AND** 服务端广播 `G2C_EntityMovedNotify`
- **AND** 客户端 B 收到该通知
- **THEN** 客户端 B 将同一 entity 坐标更新为服务端最终坐标

#### Scenario: 发起者同时处理响应和通知
- **WHEN** 客户端 A 发起合法移动
- **AND** 客户端 A 收到 `G2C_MoveResponse Success=true`
- **AND** 客户端 A 收到 `G2C_EntityMovedNotify`
- **THEN** 客户端 A 最终坐标与服务端最终坐标一致
- **AND** 重复应用不会把坐标推到其他位置

#### Scenario: 非法移动不影响 B
- **WHEN** 客户端 A 发起非法移动
- **AND** 服务端回复 A 的 `G2C_MoveResponse Success=false`
- **THEN** 客户端 B 不应用新的 `G2C_EntityMovedNotify`
- **AND** 客户端 B 上该 entity 坐标保持为非法移动前的服务端确认坐标

### Requirement: Minestom 广播参考边界
客户端 SHALL 只接入服务端推送的最小移动结果，不移植 Minestom 的 AOI、packet 或线程模型。

#### Scenario: 使用 observer/session 集合替代完整 AOI
- **WHEN** 客户端参与第一版双客户端同步
- **THEN** 客户端只期望收到在线集合广播的 `G2C_EntityMovedNotify`
- **AND** 不要求客户端维护 chunk view distance 或 add/remove 可见性差量

#### Scenario: 不引入客户端预测或插值
- **WHEN** 客户端处理 `G2C_EntityMovedNotify`
- **THEN** 客户端直接应用服务端最终坐标
- **AND** 不在本变更中实现预测、回滚或插值

### Requirement: Unity 客户端显示多个玩家
Unity 客户端 SHALL 能在同一个客户端 world 中同时存在并显示本地 player entity、远端 player entity、自动移动球和阻挡体。

#### Scenario: 收到远端玩家 snapshot
- **WHEN** 客户端收到一个不属于本地玩家的 player entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该远端 player entity

#### Scenario: 收到远端玩家移动通知
- **WHEN** 客户端收到远端 player entity 的服务端最终状态
- **THEN** 客户端把该远端 player entity 更新到服务端最终坐标
- **AND** 客户端不执行本地移动规则来推导远端坐标

#### Scenario: 收到自动移动球 snapshot
- **WHEN** 客户端收到自动移动球 entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该球 entity
- **AND** 显示层能根据组件/tag 或配置标识把它显示为球

#### Scenario: 收到阻挡体 snapshot
- **WHEN** 客户端收到阻挡体 entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该阻挡体 entity
- **AND** 显示层能根据组件/tag 或配置标识显示阻挡体

### Requirement: 客户端显示进入推动地格
Unity 客户端 SHALL 能通过服务端 snapshot/delta 创建或更新传送带这类进入推动地格的镜像 entity，并使用配置标识或 tag 选择最小显示方式。

#### Scenario: 收到传送带 snapshot
- **WHEN** 客户端收到传送带 entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该 mirror entity
- **AND** 该 mirror entity 的坐标来自服务端 snapshot
- **AND** 客户端显示层能把它和玩家、球、阻挡体区分开

#### Scenario: 客户端不本地裁决推动
- **WHEN** 本地玩家站上传送带显示格
- **THEN** 客户端不直接修改权威玩家坐标
- **AND** 客户端等待服务端 WorldDelta 应用最终坐标

### Requirement: 客户端应用删除 delta
Unity 客户端 SHALL 在处理服务端 `G2C_WorldDeltaNotify` 时应用 removed entity id，从当前 `ClientMapWorld` 中移除对应镜像实体。

#### Scenario: 收到删除 delta 后移除镜像实体
- **WHEN** 客户端收到 `G2C_WorldDeltaNotify`
- **AND** notify 的 `RemovedEntityIds` 包含一个当前存在的 entity id
- **THEN** 客户端从当前 `ClientMapWorld` 移除该 mirror entity
- **AND** 显示层不再展示该 entity

#### Scenario: 删除不存在镜像实体保持幂等
- **WHEN** 客户端收到 `RemovedEntityIds`
- **AND** 某个 removed id 在当前 `ClientMapWorld` 中不存在
- **THEN** 客户端忽略该 removed id
- **AND** delta handler 不抛异常

#### Scenario: 删除先于变更应用
- **WHEN** 同一 `G2C_WorldDeltaNotify` 同时包含 `RemovedEntityIds` 和 changed `Entities`
- **THEN** 客户端先应用删除集合
- **AND** 再应用 changed entity snapshots
- **AND** 若同一 entity id 被删除后又出现在 changed entities 中，最终以 changed snapshot 重建或更新后的状态为准

### Requirement: Unity 权威调试工具 UI
Unity Demo SHALL 提供一个 Play Mode 可用的 Runtime 调试 UI，用于选择格子、建造实体、拖拽实体和删除实体。该工具 SHALL 只向服务端提交调试意图，MUST NOT 直接绕过服务端修改权威客户端镜像。该工具 MUST NOT 实现正式玩家背包、资源消耗、建造距离、冷却或拥有权规则。

#### Scenario: 快捷栏选择调试工具
- **WHEN** 开发者打开调试编辑工具
- **THEN** 工具显示底部快捷栏
- **AND** 快捷栏至少包含阻挡体、球、传送带、拖拽和删除槽位
- **AND** 当前槽位在界面中高亮

#### Scenario: 数字键切换快捷栏
- **WHEN** 开发者按下快捷栏对应数字键
- **THEN** 调试 UI 切换到对应槽位
- **AND** 后续鼠标左键执行该槽位对应的调试操作

#### Scenario: 选择实体配置并建造
- **WHEN** 开发者选择阻挡体、球或传送带槽位
- **AND** 开发者点击一个地图格
- **THEN** 客户端发送调试建造 RPC
- **AND** 客户端在收到服务端响应或 delta 前不直接创建权威镜像实体

#### Scenario: 建造 ghost 和格子高亮
- **WHEN** 鼠标指向地图格
- **AND** 当前槽位是可建造实体
- **THEN** 调试 UI 显示目标格高亮框
- **AND** 调试 UI 显示当前实体的 ghost 预览
- **AND** ghost 预览不写入 `ClientMapWorld`

#### Scenario: 旋转当前建造方向
- **WHEN** 当前槽位是带方向的可建造实体
- **AND** 开发者按下 `R`
- **THEN** 调试 UI 旋转当前建造方向
- **AND** 后续调试建造 RPC 携带旋转后的方向

#### Scenario: 拖拽实体提交调试传送
- **WHEN** 当前槽位是拖拽
- **AND** 开发者选择一个已存在 entity 并释放到目标格
- **THEN** 客户端发送调试拖拽 RPC
- **AND** 该请求不通过普通 `C2G_MoveRequest`
- **AND** 客户端等待服务端 WorldDelta 应用最终坐标

#### Scenario: 拖拽选中反馈
- **WHEN** 当前槽位是拖拽
- **AND** 开发者按住一个已存在 entity
- **THEN** 调试 UI 显示该 entity 的选中状态
- **AND** 调试 UI 显示目标格反馈

#### Scenario: 删除实体提交调试删除
- **WHEN** 当前槽位是删除
- **AND** 开发者选择一个已存在 entity
- **THEN** 客户端发送调试删除 RPC
- **AND** 客户端等待服务端 removed delta 后移除镜像实体

#### Scenario: 无 session 时不提交调试请求
- **WHEN** 工具无法取得有效 Fantasy session
- **AND** 开发者尝试建造、拖拽或删除
- **THEN** 客户端不发送调试 RPC
- **AND** 工具显示可验证的失败状态

#### Scenario: 调试请求失败显示 reason
- **WHEN** 服务端拒绝调试编辑请求
- **THEN** 工具显示最近一次失败 reason
- **AND** 客户端本地 world 不因为失败请求发生权威状态变化

### Requirement: 双客户端调试编辑端到端验收
系统 SHALL 提供手动端到端验证路径，证明一个客户端发起的权威调试编辑会同步到另一个在线客户端。

#### Scenario: A 建造后 B 同步显示
- **WHEN** 服务端运行且客户端 A、B 均已 Join
- **AND** A 使用调试工具建造阻挡体
- **THEN** B 收到服务端 WorldDelta
- **AND** B 显示该阻挡体

#### Scenario: A 拖拽后 B 同步坐标
- **WHEN** 服务端运行且客户端 A、B 均已 Join
- **AND** A 使用调试工具拖拽一个球或传送带到新坐标
- **THEN** B 收到服务端 WorldDelta
- **AND** B 上同一 entity 的显示坐标与服务端日志一致

#### Scenario: A 删除后 B 同步消失
- **WHEN** 服务端运行且客户端 A、B 均已 Join
- **AND** A 使用调试工具删除一个调试 entity
- **THEN** B 收到包含该 entity id 的 removed WorldDelta
- **AND** B 不再显示该 entity

#### Scenario: 关闭调试开关后端到端拒绝
- **WHEN** 服务端调试编辑开关关闭
- **AND** A 尝试建造、拖拽或删除
- **THEN** A 收到失败 reason
- **AND** B 不收到由该请求产生的世界变化

