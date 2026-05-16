# Change: 完善运行时调试布局管理

## Why
当前运行时调试面板的保存、加载、最近选中和默认文件状态不够清晰，开发者保存后再加载时容易不确定实际使用的是最近保存的文件、最近选中的文件，还是默认 `debug-selection` 文件。这个问题会让调试结构块复现不可信，也会放大 runtime effect、port、方向等状态验证的误判。

## What Changes
- 在 Play Mode 运行时 Debug Panel 中新增明确的调试布局管理区，显示当前加载布局、保存目标名、最近操作结果和文件元信息。
- 为 `Assets/DebugLayouts/*.dgdebuglayout.json` 提供运行时增删查改能力：新建/另存为、覆盖保存、重命名、删除、刷新列表、选择加载。
- 点击布局列表项必须立即加载该布局并更新结构块 ghost 预览；覆盖、删除和重命名默认作用于当前加载布局，避免隐式使用最近保存或默认文件。
- 加载后只更新结构块 ghost 预览和加载状态，不直接实例化；实例化仍由开发者在地图 anchor 上确认。
- 保留调试资产边界：布局 JSON 仍只作为开发调试和测试复现资产，不进入 Luban 或正式关卡配置。

## Impact
- Affected specs: `client-world-runner`
- Affected code: `Client/DG_Client/Assets/Scripts/ClientWorld/DebugTools/Runtime/DGDebugPanelController.cs`, `DebugLayoutTooling`, `DebugStructureBlockStorage`, Unity EditMode tests
- Manual validation: 需要用户在 Play Mode 手动覆盖保存、加载、重命名、删除、刷新和结构块放置流程
