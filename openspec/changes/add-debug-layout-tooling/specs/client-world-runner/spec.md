## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: 调试结构块持久化
系统 SHALL 提供可验证的调试结构块持久化格式，用于保存、加载和反序列化开发调试结构。结构块 MUST 保留实体相对布局、方向和 port mask，并 MUST 通过当前实体配置 provider 校验 configId。结构块 SHALL 属于调试和测试复现资产，MUST NOT 成为正式运行时配置源。

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
