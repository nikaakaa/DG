## 1. 状态模型
- [x] 1.1 梳理运行时 Debug Panel 中保存名、当前加载文件、默认文件和最近操作结果的现有字段。
- [x] 1.2 增加明确的布局管理状态模型，区分 `currentLayoutPath`、`saveTargetName`、`lastSavedPath` 和 `lastLayoutOperationResult`。
- [x] 1.3 保证点击列表项立即加载对应布局并更新 ghost 预览，保存操作不隐式改变当前加载布局。

## 2. 文件 CRUD
- [x] 2.1 增加运行时布局文件列表刷新，显示文件名、实体数量、最后修改时间和校验状态。
- [x] 2.2 实现新建/另存为：从当前选择集写入输入名称对应文件，保存后刷新列表并立即加载新文件；名称归一化扩展名和非法字符。
- [x] 2.3 实现覆盖保存：只覆盖当前加载布局文件；没有当前加载布局时拒绝并显示 reason。
- [x] 2.4 实现重命名：只重命名当前加载布局文件，处理空名、非法名、目标已存在并同步当前加载路径；重名时拒绝。
- [x] 2.5 实现删除：只删除当前加载布局文件，删除后清理当前加载/预览状态并刷新列表。
- [x] 2.6 实现点击加载：点击列表项后立即加载该文件为结构块 ghost 预览，并显示当前加载文件；加载失败时保留原当前加载布局。

## 3. 运行时 UI
- [x] 3.1 在 Runtime Debug Panel 增加布局管理分区，避免保存/加载按钮和工具槽位混在一起。
- [x] 3.2 显示当前保存目标、当前加载布局、选择集数量和最近操作结果。
- [x] 3.3 按钮文案必须带目标语义，例如“覆盖当前加载”“另存为输入名”“重命名当前加载”“删除当前加载”。
- [x] 3.4 文件列表支持刷新，且刷新不改变当前加载预览，除非对应文件已不存在。
- [x] 3.5 文件列表按最后修改时间倒序排列，并高亮当前加载布局。
- [x] 3.6 文件列表显示短名称，状态区显示完整相对路径。
- [x] 3.7 布局管理放在独立 Layouts 区域或 Structure Block 区域附近，不占用底部快捷栏槽位。

## 4. 测试
- [x] 4.1 Unity TestFramework EditMode：保存 A、保存 B、点击 A 后立即加载 A，点击 B 后立即加载 B。
- [x] 4.2 Unity TestFramework EditMode：重命名后文件列表和当前加载路径一致更新。
- [x] 4.3 Unity TestFramework EditMode：删除当前加载文件后清理 current layout，并且不会实例化旧结构。
- [x] 4.4 Unity TestFramework EditMode：刷新列表不会把最近保存文件自动设为当前加载文件。
- [x] 4.5 Unity TestFramework EditMode：非法名称、空选择保存/覆盖、目标已存在时返回可读 reason。
- [x] 4.6 Unity TestFramework EditMode：点击损坏布局文件失败后保留原当前加载布局。
- [x] 4.7 Unity TestFramework EditMode：删除当前加载布局后不会自动加载其他布局。
- [x] 4.8 Unity TestFramework EditMode：刷新列表不因外部文件修改自动重载 ghost。

## 5. 验证
- [x] 5.1 运行 `openspec validate update-runtime-debug-layout-manager --strict --no-interactive`。
- [x] 5.2 运行相关 Unity EditMode 测试。
- [ ] 5.3 用户手动验证：Play Mode 中保存 A、保存 B、点击 A、点击 B、覆盖当前加载、重命名当前加载、删除当前加载，并确认预览和实例化始终对应界面显示的当前加载文件。
