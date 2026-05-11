## Context

现有调试能力分成两块：

- Play Mode UI：`ClientWorldDebugEditor` / `DGDebugPanelController` 可以单格建造、选择、拖拽、删除，并向服务端提交调试 RPC。
- 测试脚本：`SandboxScenarioDocument` / `SandboxScenarioStorage` 可以把脚本化步骤保存成 JSON，并由 `LocalSandboxScenarioRunner` 本地验证。

缺口是二者没有闭环。开发者在 Unity 中手工搭出的结构不能直接保存为一个可复用对象，也不能反序列化回调试 UI 快速再次放置。批量选择、批量移动和复制结构也缺少统一数据模型，导致每次复现复杂推动、port 连体、传送带和阻挡组合都要重复点很多次。

这件事不只是“多几个按钮”。在建造类系统里，批量选择形成结构块、结构块持久化、之后再作为蓝图放回世界，是很常见的工具链。当前鼠标控制还很简单，所以第一版应在调试层做出稳定的数据模型和交互边界，而不是急着做正式建造系统。

另一个现实缺口是 port 可视化。`ClientWorldVisuals` 目前已经能给 `PortConnectorBlocker` 画线，但它主要依据 `configId` 和固定左右端口推断显示。随着 runtime effect 可以改变最终 `PortConnectorComponent`，调试视图需要显示服务端同步后的最终 `PortLocalPorts`，并显示朝向转换后的世界端口和相邻连接关系，否则开发者很难判断 port 结构块是否真的连接成功。

## Goals

- 让调试结构块可保存、可加载、可反序列化回调试工具并验证。
- 结构块保留实体配置、方向、相对坐标、port mask 和必要调试元数据。
- 批量选择使用当前客户端镜像作为选择来源，但批量变更必须提交到服务端权威路径。
- 复用已有 `SandboxScenario` 存储和校验能力，避免产生第二套无关 JSON 格式。
- 第一版只支持平移复制，不支持旋转、镜像和嵌套分组。
- port 调试可视化读取最终同步状态，不根据 configId 或实体名字猜测当前 port。

## Non-Goals

- 不做正式地图编辑器。
- 不把调试布局提升为 Luban 配置源。
- 不让客户端直接裁决碰撞、推动或占用规则。
- 不解决多用户同时编辑冲突，只要求请求失败时可见且不污染本地权威镜像。
- 不把 port 可视化接入规则判断；规则仍然只在 Shared/服务端权威路径中执行。

## Decisions

### Decision: 结构块是调试蓝图对象，不是正式配置源

新增持久化对象命名上可以叫 layout、blueprint 或 structure block，但语义上是“从选择集导出的调试结构块”。它应尽量落在 `SandboxScenarioDocument` 的现有语义上，或者作为它的兼容扩展，而不是新增完全独立格式。模板中的实体记录应能表达：

- alias
- configId
- x / y
- direction
- playerId
- autoMoveIntervalTicks
- final/static port mask

保存时使用相对坐标：选区左下或显式 anchor 作为原点，模板记录每个实体相对原点的 offset。加载到目标格时再还原为绝对坐标。

这个结构块格式可以成为未来正式建造系统的概念原型，但当前 change 只保证调试用途。正式系统需要另行定义合法性、资源、拥有权、版本迁移、保存位置和发布流程。

第一版结构块只保存实体基础状态和最终 port mask，不保存 runtime effect 实例、剩余过期 tick、临时 tag 调试步骤或其他运行时生命周期状态。原因是 runtime effect 属于时间相关的调试操作，直接固化进结构块会让“结构复现”和“运行时状态复现”混在一起。后续如果需要复现 runtime effect，应作为结构块附带的可选调试脚本或 scenario step 单独扩展。

### Decision: 鼠标控制升级为选择集状态机

当前鼠标控制偏单点：指向一个格子，点击后执行当前工具。结构块工作流需要至少引入调试侧状态机：

- 空闲/单格工具
- 框选/追加选择
- 选择集预览
- 拖动选择集
- 结构块 ghost 放置
- 批量提交等待结果

第一版不需要做复杂编辑器框架，但数据层必须先支持选择集和结构块，UI 可以在这个状态机上逐步加能力。

### Decision: 批量工具只生成调试意图

批量移动、批量复制和模板实例化不得直接写 `ClientMapWorld`。它们应生成一组调试请求：

- 复制或实例化：多条 debug spawn 请求。
- 批量移动：多条 debug move 请求，或未来服务端提供的批量调试请求。
- 批量删除：多条 debug remove 请求。

第一版可以按顺序提交单体请求，只要 UI 明确显示部分失败，并且失败不会让客户端本地镜像先行变更。若后续需要原子批量提交，应另开 change 增加服务端批量协议。

### Decision: 选择集是客户端辅助状态

批量选择集只存在于 Unity 调试工具内，用 entity id 和当前镜像坐标描述。选择集可以通过框选、逐个添加、清空和反选维护。选择集失效时，例如 entity 已被服务端删除，工具必须跳过该实体并显示原因。

### Decision: port 可视化读取最终 component 状态

port 可视化应从 `EntitySnapshot.PortLocalPorts` 或客户端镜像中的最终 `PortConnectorComponent` 读取端口，而不是只根据 `DefaultWorldConfig.PortConnectorBlockerConfigId` 推断。显示层需要把 local port mask 按实体方向转换为 world port mask，并以可见方式显示：

- 每个实体当前拥有的本地/世界端口方向。
- 相邻实体之间是否存在匹配 port 连接。
- 运行时 port effect 叠加后的最终端口，而不是静态配置端口。
- 选择结构块时，结构块内部 port 连接和外部可接端口。

这仍然是显示和调试辅助，不把连接判断复制成客户端权威规则。

### Decision: 模板资产属于调试目录

默认保存路径使用 `Client/DG_Client/Assets/DebugLayouts`。该目录只服务于开发调试和测试复现，不参与正式配置导出。

不默认使用 `StreamingAssets`，因为当前项目已经用 `StreamingAssets/GameConfig` 承载运行时读取的 Luban 配置。把调试结构块放进 `StreamingAssets` 容易让它看起来像正式运行时数据源。后续如果需要从运行时外部目录导入结构块，可以增加显式 import/export 路径，而不是改变默认保存目录。

结构块文件扩展名使用 `.dgdebuglayout.json`。它仍然是普通 JSON，方便 diff、手改和测试读取；同时文件名能明确标识这是 DG 调试布局，不是 Luban JSON、正式地图数据或通用 sandbox scenario。

## Risks / Trade-offs

- 顺序提交多条 RPC 不具备原子性，可能出现部分成功。第一版接受该限制，并要求 UI 显示每条失败原因；原子批量协议留到后续。
- 复用 `SandboxScenario` 可以减少格式分裂，但可能需要扩展它以表达选区 anchor 和布局元数据。
- 从客户端镜像导出模板依赖当前镜像状态。如果本地 mirror 已经落后，导出的模板也会落后；手动端到端验证必须确认服务端同步链路正常。
- port 可视化如果只画得好看但不绑定最终 component 状态，会继续误导调试。因此测试要覆盖 runtime port mask 显示来源。

## Open Questions

- 暂无。第一版已固定为基础结构块持久化；runtime effect 复现、外部导入目录和正式建造蓝图迁移留给后续 change。
