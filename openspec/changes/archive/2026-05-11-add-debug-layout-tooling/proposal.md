# Change: 增强调试布局搭建工具

## Why

当前 Unity 调试工具已经支持单个实体的建造、选择、拖拽、删除和部分 runtime effect 操作，但开发者搭一组测试结构时仍需要重复手工摆放。已有 `SandboxScenario` JSON 能保存本地测试脚本，但还缺少从 Play Mode 可视化搭建结果导出、复用、反序列化还原和批量编辑的工具链。

本次变更目标是让一组选中的调试实体可以成为一个“结构块/蓝图”对象，被持久化保存，之后再反序列化回调试工具快速建造。这个形态也为后续正式建造系统保留一条常见路径：从单格鼠标操作，升级到选择集、结构块、蓝图预览和批量提交。

## What Changes

- 扩展 Unity 调试工具，使开发者可以批量选择实体，并把选择集保存为可持久化结构块/蓝图。
- 增加结构块反序列化和快速实例化入口，实例化仍通过服务端权威调试 RPC 或等价服务端调试路径提交。
- 增加批量选择、批量移动、复制结构和结构块 ghost 预览能力，操作单位是调试实体集合及其相对坐标。
- 增加 port 调试可视化，使开发者能看到实体最终 port mask、朝向后的世界端口和相邻 port 连接关系。
- 将可复用结构块数据定位为调试/测试资产，并明确它是未来建造系统蓝图形态的候选前身，但当前不作为正式 Luban 实体配置或正式关卡数据来源。
- 补充 Unity TestFramework EditMode 覆盖布局序列化、相对坐标、批量操作请求生成和错误场景。
- 保留用户手动 Play Mode 端到端验证路径，确认一个客户端批量搭建或复制后，另一个客户端能看到服务端同步结果。

## Non-Goals

- 不实现正式关卡编辑器、地图保存格式、生产配置发布流程或 Luban 表编辑。
- 不绕过服务端权威规则直接写 `ClientMapWorld`。
- 不增加正式玩家建造规则，例如资源、背包、冷却、拥有权或建造距离。
- 不在本次实现任意旋转、镜像、分组层级嵌套、结构块版本迁移 UI 或复杂 prefab 变体系统。
- 不把 port 可视化做成规则裁决入口；可视化只读取服务端同步后的最终 port/component 状态。
- 不执行 Unity Player build。

## Impact

- Affected specs:
  - `client-world-runner`
- Affected code:
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Debug/Runtime/ClientWorldDebugEditor.cs`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/Debug/Runtime/DGDebugPanelController.cs`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/View/ClientWorldVisuals.cs`
  - `Shared/DG.GameCore/Testing/SandboxScenario.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/SandboxScenarioTests.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/ClientMoveNetworkRuntimeTests.cs`
