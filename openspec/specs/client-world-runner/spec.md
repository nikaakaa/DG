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
Unity Demo SHALL 提供一个 Play Mode 可用的 Runtime 调试 UI，用于选择格子、建造实体、拖拽实体、删除实体、保存调试结构块、加载调试结构块、批量选择、批量移动、复制结构和查看 port 调试可视化。该工具 SHALL 只向服务端提交调试意图，MUST NOT 直接绕过服务端修改权威客户端镜像。该工具 MUST NOT 实现正式玩家背包、资源消耗、建造距离、冷却或拥有权规则。调试结构块 SHALL 仅作为开发调试和测试复现资产，MUST NOT 替代 Luban 实体配置或正式关卡数据。

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

#### Scenario: 保存选区为调试结构块
- **WHEN** 开发者在调试 UI 中选择多个已同步 entity
- **AND** 开发者保存当前选区为结构块
- **THEN** 工具写入一个调试结构块 JSON
- **AND** JSON 记录每个 entity 的 configId、方向、port mask 和相对坐标
- **AND** 模板不写入 Luban 配置或正式关卡配置

#### Scenario: 加载结构块并预览
- **WHEN** 开发者选择一个已保存的调试结构块
- **AND** 鼠标指向目标 anchor 格
- **THEN** 调试 UI 显示结构块 ghost 预览
- **AND** 预览使用模板实体的相对坐标还原结构
- **AND** 预览显示结构块内部 port 连接和外部可接端口
- **AND** 预览不写入 `ClientMapWorld`

#### Scenario: 反序列化并实例化调试结构块
- **WHEN** 开发者从 JSON 反序列化调试结构块
- **AND** 开发者确认在目标 anchor 实例化结构块
- **THEN** 客户端为模板中的实体提交调试建造请求
- **AND** 每个请求携带由目标 anchor 和相对坐标计算出的绝对坐标
- **AND** 客户端等待服务端响应或 WorldDelta 后显示最终权威实体

#### Scenario: 鼠标控制进入结构块放置状态
- **WHEN** 开发者加载一个调试结构块
- **THEN** 调试 UI 进入结构块 ghost 放置状态
- **AND** 鼠标移动会更新结构块 anchor 和整体预览
- **AND** 确认放置前不会提交调试建造请求

#### Scenario: 批量选择实体
- **WHEN** 开发者使用框选或追加选择多个已同步 entity
- **THEN** 调试 UI 维护一个选择集
- **AND** 选择集显示 entity 数量和 anchor
- **AND** 若选择集中的 entity 已被服务端删除，工具在下一次批量操作前将其标为失效

#### Scenario: 批量移动选择集
- **WHEN** 开发者选中多个 entity
- **AND** 开发者把选择集平移到目标位置
- **THEN** 客户端为仍有效的 entity 提交调试移动请求
- **AND** 每个请求保留选择集内的相对布局
- **AND** 客户端不在服务端响应前直接移动本地权威镜像

#### Scenario: 复制选择集结构
- **WHEN** 开发者选中多个 entity
- **AND** 开发者复制结构到目标 anchor
- **THEN** 客户端为选择集中的每个有效 entity 提交调试建造请求
- **AND** 新结构保留原选择集的相对坐标和方向
- **AND** 原结构不因复制操作被移动或删除

#### Scenario: 批量操作显示部分失败
- **WHEN** 批量移动、复制或模板实例化中的部分请求被服务端拒绝
- **THEN** 调试 UI 显示成功数、失败数和最近失败 reason
- **AND** 被拒绝的请求不产生客户端本地伪状态
- **AND** 已成功的请求仍以服务端 WorldDelta 为准同步

#### Scenario: port 调试可视化
- **WHEN** 开发者打开调试 UI 并查看带 `PortConnectorComponent` 的 entity
- **THEN** 调试显示读取服务端同步后的最终 port mask
- **AND** 调试显示将 local port mask 按 entity direction 转换后的 world port 方向
- **AND** 调试显示相邻实体之间的匹配 port 连接关系
- **AND** 调试显示不通过 configId、entity name 或客户端本地 runtime effect 推断权威连接结果

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

### Requirement: Unity Client GameCore Reference
Unity client SHALL be able to compile and execute the shared `DG.GameCore` rule layer for tests and later prediction.

#### Scenario: Unity references GameCore package
- **WHEN** Unity client code references `DG.GameCore`
- **THEN** the client project compiles through a local package reference to Shared GameCore
- **AND** the client does not copy Shared source into a second divergent implementation
- **AND** Shared GameCore remains independent of Unity runtime APIs

#### Scenario: Unity test executes shared movement rule
- **WHEN** a Unity TestFramework EditMode test creates a `GameWorld` with a movable entity and a blocking entity
- **THEN** the test can execute the state-driven rule system
- **AND** the result matches the same component-driven blocking behavior used by the server

### Requirement: Client World Rule Truth Boundary
Unity client SHALL treat Shared GameCore state as the rule truth for shared-rule execution, and Unity-specific world objects MUST NOT retain a separate rule implementation.

#### Scenario: Snapshot updates shared mirror state
- **WHEN** Unity client receives a server world snapshot or delta
- **THEN** the client can apply the authoritative entity state into a Shared GameCore mirror world
- **AND** Unity visual state is updated from that mirror

#### Scenario: Unity-only world is removed from rule decisions
- **WHEN** an interaction depends on movement, blocking, bouncing, or spatial occupancy rules
- **THEN** the rule decision is made by Shared GameCore when running shared-rule mode
- **AND** Unity-only `World`, `EntityTracker`, or `DirtyTracker` does not remain as a parallel rule implementation

#### Scenario: Prediction remains out of scope
- **WHEN** this migration is implemented
- **THEN** the client is not required to perform prediction, rollback, or reconciliation
- **AND** the migration only establishes the shared rule/runtime foundation required by later prediction work

### Requirement: 客户端本地 legacy 规则迁移边界
Unity 客户端 SHALL NOT keep the legacy `MovementResolveSystem` as the default authority for server-authoritative movement. If a local movement system remains for offline tests or non-authoritative tooling, it MUST be explicitly separated from the server-authoritative runtime path and MUST NOT update authoritative mirror state while server-authoritative mode is enabled.

#### Scenario: 服务端权威模式不执行本地 legacy 移动裁决
- **WHEN** Unity 客户端处于 server-authoritative movement mode
- **AND** the local player submits movement input
- **THEN** the client sends the movement request through the network submitter
- **AND** the local legacy movement resolver does not update the authoritative mirror position before server response or world delta

#### Scenario: 本地辅助路径必须显式标识
- **WHEN** a local movement system remains for EditMode tests, sandbox local mode, or offline debugging
- **THEN** it is named and wired as a non-authoritative helper
- **AND** tests distinguish it from the server-authoritative world mirror path

### Requirement: Unity ClientWorld Source Layout
Unity client world runtime code SHALL live under a folder that represents the client world module instead of a generic map module.

#### Scenario: Client world files are discoverable
- **WHEN** a developer looks for client world bootstrap, runtime tick, networking, view, debug, input, interaction, or spatial adapter code
- **THEN** those files are discoverable under `Client/DG_Client/Assets/Scripts/ClientWorld`

#### Scenario: Map naming does not hide world mirror responsibility
- **WHEN** a developer reviews the client runtime directory
- **THEN** the folder naming makes it clear that the module mirrors and displays server-authoritative world state
- **AND** it is not presented as a local gameplay rule authority

### Requirement: ClientWorld Migration Preserves Server Authority
The client world folder migration SHALL preserve the existing server-authoritative movement and sync behavior.

#### Scenario: Directory migration does not reintroduce local movement authority
- **WHEN** the client world runner advances a tick
- **THEN** it does not locally resolve movement rules through a legacy movement resolver
- **AND** player coordinate changes still come from server response, snapshot, or delta application

#### Scenario: Unity references survive migration
- **WHEN** Unity refreshes scripts after the folder migration
- **THEN** `ClientWorldRunner`, networking submitter, view, debug, and test scripts remain compilable
- **AND** EditMode tests can run through Unity TestFramework without requiring a Unity Player build

### Requirement: 客户端镜像最终 Component 结果
Unity 客户端 SHALL mirror server-authoritative final Component results from snapshot/delta. `ClientMapWorld` MUST NOT maintain a local runtime effect resolver, MUST NOT activate ability state locally, MUST NOT merge dynamic ports locally, and MUST NOT compute final Component results or movement permission from runtime effect data.

#### Scenario: Snapshot applies final result
- **WHEN** the client receives a server snapshot or delta that contains final `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, or movement permission state
- **THEN** `ClientMapWorld` applies that final state into its Shared `GameWorld` mirror
- **AND** Unity view code reads the mirrored final state for display

#### Scenario: Client does not resolve effect
- **WHEN** a server-side runtime effect creates or removes temporary component results
- **THEN** the client waits for server snapshot/delta carrying the final result
- **AND** the client does not evaluate `RuntimeEffectSpec`, `RuntimeEffectInstance`, `AbilityKind`, or `EffectKind` locally to decide authority state

#### Scenario: Client does not merge port or movement permission
- **WHEN** a server-side runtime effect changes port connectivity or movement permission
- **THEN** the client applies only the server-synchronized final result
- **AND** the client does not locally merge port sources
- **AND** the client does not locally decide whether movement should be accepted or rejected

#### Scenario: Manual observer sees final result
- **WHEN** client A applies and removes a temporary runtime effect through an approved debug or test path
- **AND** client B is observing the same server world
- **THEN** client B sees only the server-synchronized final Component result changes
- **AND** client B does not need the effect source data to display the final state

### Requirement: 客户端连接体推动整体动画表现
Unity 客户端 SHALL treat server `WorldDelta` as the only authority for which connected body members moved. When the server delta contains multiple moved connected body members, the client animation layer MUST generate and play animation for every received moved member. The client MUST NOT infer missing moved members from local port graph or local push rules.

#### Scenario: 服务端全员 delta 生成全员动画
- **WHEN** 客户端收到一个 `G2C_WorldDeltaNotify`
- **AND** notify contains changed entity snapshots for every moved member of a connected body
- **AND** notify contains animation metadata for those moved members
- **THEN** `ClientAnimationLayer` creates one animation event per moved member
- **AND** `ClientWorldVisuals` can keep active animations for all moved members at the same time

#### Scenario: 缺失成员不由客户端补齐
- **WHEN** 客户端收到的 `G2C_WorldDeltaNotify` only contains part of a connected body
- **THEN** the client applies only the authoritative snapshots present in the delta
- **AND** the client does not use local port graph, local push rules, or cached connected body membership to move additional members
- **AND** the missing-member condition is treated as a server sync correctness problem rather than a client-side rule decision

#### Scenario: metadata-only 推力反馈不改变坐标
- **WHEN** 客户端收到一个 `G2C_WorldDeltaNotify`
- **AND** notify contains no changed entity snapshots
- **AND** notify contains mechanism push animation metadata for one or more entities
- **THEN** `ClientAnimationLayer` creates impulse animation events for those entities
- **AND** `ClientWorldVisuals` plays push feedback for those entities
- **AND** the client does not change any entity coordinate because of that metadata-only delta

#### Scenario: 连续 delta 后全员落到最新服务端状态
- **WHEN** the client receives consecutive server deltas for multiple connected body members
- **THEN** each moved member eventually reaches its latest authoritative coordinate
- **AND** later deltas replace older active animations per entity without losing other members' animations

### Requirement: 调试结构块持久化
系统 SHALL 提供可验证的调试结构块持久化格式，用于保存、加载和反序列化开发调试结构。结构块 MUST 保留实体相对布局、方向和 port mask，并 MUST 通过当前实体配置 provider 校验 configId。第一版结构块 MUST NOT 保存 runtime effect 实例、剩余过期 tick 或临时调试 tag 状态。结构块 SHALL 属于调试和测试复现资产，MUST NOT 成为正式运行时配置源。

#### Scenario: 保存后加载保持结构块
- **WHEN** 一个包含多个实体的调试结构块被保存为 JSON
- **AND** 使用同一配置 provider 加载该结构块
- **THEN** 加载结果保留结构块名称、schema version、实体数量、configId、方向、port mask 和相对坐标

#### Scenario: 未知 configId 被拒绝
- **WHEN** 调试结构块包含当前配置 provider 不认识的 configId
- **THEN** 结构块加载失败
- **AND** 失败 reason 指出未知 configId
- **AND** 工具不提交任何调试建造请求

#### Scenario: 相对坐标由 anchor 计算
- **WHEN** 开发者从选择集导出调试结构块
- **THEN** 模板以选择集 anchor 为原点保存每个实体的相对坐标
- **AND** 模板加载到新 anchor 时按相同相对坐标恢复结构

#### Scenario: 反序列化失败不污染工具状态
- **WHEN** 开发者加载一个格式错误或 schema 不兼容的结构块 JSON
- **THEN** 反序列化失败并返回可读 reason
- **AND** 当前选择集、当前工具模式和 `ClientMapWorld` 保持不变

#### Scenario: 默认保存路径和扩展名
- **WHEN** 开发者保存调试结构块
- **THEN** 工具默认保存到 `Assets/DebugLayouts`
- **AND** 文件扩展名使用 `.dgdebuglayout.json`
- **AND** 文件不会写入 `StreamingAssets/GameConfig`

#### Scenario: runtime effect 不随结构块保存
- **WHEN** 选择集中的 entity 带有 runtime effect 或临时调试 tag 状态
- **AND** 开发者导出调试结构块
- **THEN** 结构块只保存实体基础状态、相对坐标、方向和 port mask
- **AND** runtime effect 实例、剩余过期 tick 和临时 tag 调试状态不会进入结构块

### Requirement: 调试结构块和 port 可视化验证
系统 SHALL 为调试结构块、选择集导出、结构块实例化、批量移动、复制结构和 port 调试可视化提供 Unity TestFramework EditMode 覆盖，并 SHALL 提供手动端到端验证路径证明服务端权威同步仍然成立。

#### Scenario: EditMode 覆盖结构块序列化
- **WHEN** 运行相关 Unity TestFramework EditMode 测试
- **THEN** 测试覆盖保存后加载、反序列化失败、未知 configId 拒绝、anchor 相对坐标和结构块实例化坐标计算

#### Scenario: EditMode 覆盖批量操作边界
- **WHEN** 运行相关 Unity TestFramework EditMode 测试
- **THEN** 测试证明批量移动和复制只生成调试请求
- **AND** 测试证明它们不直接修改 `ClientMapWorld`
- **AND** 测试覆盖失效 entity 被跳过并记录 reason

#### Scenario: EditMode 覆盖 port 可视化来源
- **WHEN** 运行相关 Unity TestFramework EditMode 测试
- **THEN** 测试证明 port 可视化读取最终 port mask
- **AND** 测试证明 runtime port effect 改变最终端口时，可视化数据随服务端同步结果变化
- **AND** 测试证明可视化不只按 configId 推断静态端口

#### Scenario: 手动端到端验证
- **WHEN** 服务端运行且客户端 A、B 均已 Join
- **AND** A 通过调试结构块实例化或复制一组 port 连体结构
- **THEN** B 通过服务端 WorldDelta 看到对应结构
- **AND** A 框选多个 entity 后批量移动时，B 看到相同 entity id 的最终坐标变化
- **AND** A 给实体添加 runtime port effect 后，A 与 B 的 port 调试可视化显示一致的最终 port mask 和连接变化

