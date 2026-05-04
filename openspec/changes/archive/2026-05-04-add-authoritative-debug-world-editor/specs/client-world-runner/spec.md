## ADDED Requirements
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
