## ADDED Requirements
### Requirement: 运行时调试布局文件管理
Unity Demo SHALL 在 Play Mode Runtime Debug Panel 中提供明确的调试布局文件管理能力，用于列出、新建、另存为、覆盖、重命名、删除、刷新和点击加载 `Assets/DebugLayouts/*.dgdebuglayout.json`。布局管理 MUST 明确区分当前加载布局、保存目标名和最近操作结果，MUST NOT 通过最近保存或默认文件隐式决定加载、覆盖、重命名或删除目标。该能力 MUST 只管理调试结构块资产，MUST NOT 写入 Luban 配置、正式关卡配置或 `StreamingAssets/GameConfig`。

#### Scenario: 列出调试布局文件
- **WHEN** 开发者打开运行时 Debug Panel 的布局管理区
- **THEN** UI 显示 `Assets/DebugLayouts` 下所有 `.dgdebuglayout.json` 文件
- **AND** 每个条目显示文件名、实体数量、最后修改时间和校验状态
- **AND** 当前加载布局在 UI 中高亮显示
- **AND** 文件列表显示短名称，状态区显示当前加载布局的完整相对路径

#### Scenario: 另存为输入名称
- **WHEN** 开发者输入布局名称
- **AND** 当前选择集非空
- **AND** 开发者点击另存为
- **THEN** 工具把当前选择集保存到该名称对应的 `.dgdebuglayout.json`
- **AND** 文件列表刷新并立即加载新保存文件
- **AND** 当前加载布局路径更新为新保存文件

#### Scenario: 归一化输入布局名称
- **WHEN** 开发者输入布局名称
- **THEN** 工具去掉首尾空白并移除非法路径字符
- **AND** 开发者无需输入 `.dgdebuglayout.json` 扩展名
- **AND** 若归一化后名称为空，工具拒绝操作并显示可读 reason

#### Scenario: 另存为拒绝已存在目标
- **WHEN** 开发者输入一个已存在的布局名称
- **AND** 开发者点击另存为
- **THEN** 工具拒绝操作并显示可读 reason
- **AND** 工具不覆盖已有文件

#### Scenario: 覆盖当前加载布局
- **WHEN** 当前已有加载布局
- **AND** 当前选择集非空
- **AND** 开发者点击覆盖当前加载
- **THEN** 工具只覆盖当前加载布局文件
- **AND** 最近保存文件记录为被覆盖文件
- **AND** 工具重新校验或重新加载当前布局预览

#### Scenario: 拒绝隐式覆盖
- **WHEN** 没有当前加载布局
- **AND** 开发者点击覆盖当前加载
- **THEN** 工具拒绝操作并显示可读 reason
- **AND** 工具不写入默认文件或最近保存文件

#### Scenario: 点击列表项立即加载预览
- **WHEN** 开发者点击文件列表中的一个布局文件
- **THEN** 工具立即加载该文件为结构块 ghost 预览
- **AND** UI 显示当前加载预览文件路径
- **AND** 加载预览不提交任何调试建造请求

#### Scenario: 重命名当前加载布局
- **WHEN** 当前已有加载布局
- **AND** 输入一个合法且未占用的新名称
- **AND** 开发者点击重命名
- **THEN** 工具重命名当前加载布局文件并刷新列表
- **AND** 当前加载预览路径同步更新为新路径
- **AND** 工具不改变结构块内容

#### Scenario: 重命名拒绝已存在目标
- **WHEN** 当前已有加载布局
- **AND** 开发者输入一个已存在的布局名称
- **AND** 开发者点击重命名
- **THEN** 工具拒绝操作并显示可读 reason
- **AND** 当前加载布局路径保持不变

#### Scenario: 删除当前加载布局
- **WHEN** 当前已有加载布局
- **AND** 开发者点击删除当前加载
- **THEN** 工具删除当前加载布局文件并刷新列表
- **AND** 工具清空加载预览并退出结构块放置状态
- **AND** 工具不会继续实例化已删除文件的旧内存结构
- **AND** 工具不会自动加载列表中的其他布局

#### Scenario: 点击损坏文件加载失败
- **WHEN** 当前已有加载布局
- **AND** 开发者点击一个校验失败或反序列化失败的布局文件
- **THEN** 工具显示加载失败 reason
- **AND** 当前加载布局和 ghost 预览保持为失败前的布局

#### Scenario: 刷新列表不改变加载目标
- **WHEN** 开发者点击刷新列表
- **THEN** 工具重新扫描布局文件
- **AND** 当前加载布局仍保持不变
- **AND** 若当前加载布局文件已不存在，工具清空加载预览并显示 reason
- **AND** 若当前加载布局文件仍存在但内容被外部修改，工具只更新列表元信息，不自动重载 ghost 预览

#### Scenario: 文件列表排序和高亮
- **WHEN** 工具显示布局文件列表
- **THEN** 文件按最后修改时间倒序排列
- **AND** 当前加载布局高亮显示
- **AND** 排序不会改变当前加载布局

#### Scenario: 空选择集拒绝保存
- **WHEN** 当前选择集为空
- **AND** 开发者点击另存为或覆盖当前加载
- **THEN** 工具拒绝操作并显示可读 reason
- **AND** 工具不写入空布局文件

#### Scenario: 加载后进入放置状态
- **WHEN** 开发者点击一个有效布局文件
- **THEN** 工具立即进入结构块 ghost 放置状态
- **AND** 当前框选集保持不变
- **AND** UI 同时显示选择集数量和当前加载布局

#### Scenario: 覆盖后重载当前预览
- **WHEN** 开发者覆盖当前加载布局成功
- **THEN** 工具刷新列表并重新加载当前布局 ghost 预览
- **AND** 当前框选集保持不变

#### Scenario: 布局管理区不混入快捷栏
- **WHEN** 运行时 Debug Panel 显示布局管理能力
- **THEN** 布局管理显示在独立的 Layouts 区域或 Structure Block 区域附近
- **AND** 新建、覆盖、重命名、删除和刷新操作不占用底部工具快捷栏槽位

### Requirement: 运行时调试布局管理验证
系统 SHALL 使用 Unity TestFramework EditMode 覆盖运行时调试布局文件管理状态，并 SHALL 提供手动 Play Mode 验证路径证明保存、点击加载、覆盖、重命名和删除不会串用最近保存或默认文件。

#### Scenario: EditMode 覆盖状态分离
- **WHEN** Unity TestFramework EditMode 测试运行
- **THEN** 测试覆盖保存 A、保存 B、点击 A 立即加载 A、点击 B 立即加载 B
- **AND** 测试证明覆盖、重命名和删除只作用于当前加载布局
- **AND** 测试证明最近保存文件不参与加载目标选择

#### Scenario: EditMode 覆盖 CRUD 边界
- **WHEN** Unity TestFramework EditMode 测试运行
- **THEN** 测试覆盖另存为、覆盖、重命名、删除、刷新和点击加载
- **AND** 测试覆盖空选择、非法名称、目标已存在和删除已加载文件的失败或清理行为

#### Scenario: 手动端到端验证
- **WHEN** 用户在 Play Mode 打开运行时 Debug Panel
- **AND** 用户依次保存 A、保存 B、点击 A、点击 B、覆盖当前加载、重命名当前加载、删除当前加载
- **THEN** UI 始终显示当前加载布局和最近操作结果
- **AND** 每次 ghost 预览和确认实例化的结构都对应 UI 显示的当前加载预览文件
