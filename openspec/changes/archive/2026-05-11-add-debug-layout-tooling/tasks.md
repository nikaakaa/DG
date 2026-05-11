## 1. Proposal validation

- [x] 1.1 确认本 change 只覆盖调试结构块/蓝图、批量选择、批量移动、复制结构和 port 调试可视化，不处理正式地图编辑器。
- [x] 1.2 对照现有 `client-world-runner` 调试 UI 规格，确认新需求是增量而不是重写调试 UI。
- [x] 1.3 对照 `shared-gamecore-entity-rules` 的 port graph 边界，确认可视化不复制服务端规则裁决。
- [x] 1.4 运行 `openspec validate add-debug-layout-tooling --strict --no-interactive`。

## 2. Structure block data model

- [x] 2.1 定义调试结构块数据，包含 schema version、name、anchor、实体条目和可选元数据。
- [x] 2.2 每个实体条目记录 alias、configId、相对坐标、direction、playerId、autoMoveIntervalTicks 和 port mask。
- [x] 2.3 复用或兼容扩展 `SandboxScenarioDocument`，避免产生第二套无关 JSON 格式。
- [x] 2.4 增加模板校验，拒绝未知 configId、重复 alias 和空实体模板。
- [x] 2.5 增加保存/加载路径策略，默认保存到 `Assets/DebugLayouts`。
- [x] 2.6 增加从 JSON 反序列化回结构块对象的显式 API，并返回可读错误。
- [x] 2.7 增加结构块到调试建造步骤的转换，不直接创建实体。
- [x] 2.8 结构块文件使用 `.dgdebuglayout.json` 扩展名。
- [x] 2.9 第一版不保存 runtime effect 实例或临时调试 tag 状态。

## 3. Selection and batch operation model

- [x] 3.1 定义调试选择集，记录 entity id、当前坐标和相对坐标。
- [x] 3.2 支持单选、框选、追加选择和清空选择。
- [x] 3.3 支持从选择集生成结构块。
- [x] 3.4 支持将结构块实例化到目标 anchor。
- [x] 3.5 支持批量移动，将选择集按 offset 平移后生成调试移动请求。
- [x] 3.6 支持复制结构，将选择集按目标 anchor 生成调试建造请求。
- [x] 3.7 批量操作遇到已删除或缺失实体时跳过该实体并记录 reason。
- [x] 3.8 定义鼠标状态机，覆盖单格工具、框选、选择集拖动、结构块 ghost 放置和批量提交等待结果。

## 4. Runtime debug UI integration

- [x] 4.1 在 Play Mode 调试 UI 中加入选择集状态显示。
- [x] 4.2 加入保存当前选择为结构块的入口。
- [x] 4.3 加入加载结构块并预览 ghost 的入口。
- [x] 4.4 加入批量移动和复制结构入口。
- [x] 4.5 批量操作提交前显示目标格预览，不写入 `ClientMapWorld`。
- [x] 4.6 批量提交后显示成功数、失败数和最近失败 reason。
- [x] 4.7 增加结构块内部 port 连接预览和外部可接端口提示。

## 5. Port debug visualization

- [x] 5.1 修改 port 显示来源，优先读取服务端同步后的最终 `PortLocalPorts` / `PortConnectorComponent`。
- [x] 5.2 将 local port mask 按实体 direction 转为 world port mask 后显示。
- [x] 5.3 显示相邻实体之间的匹配 port 连接线。
- [x] 5.4 显示未连接但可对外连接的端口方向。
- [x] 5.5 显示 runtime port effect 叠加后的最终端口，不只显示静态配置端口。
- [x] 5.6 选中结构块时显示结构块内部 port 连接和边界端口。

## 6. Automated tests

- [x] 6.1 Unity TestFramework EditMode：结构块保存后再加载，实体数量、configId、direction、port mask 和相对坐标保持一致。
- [x] 6.2 Unity TestFramework EditMode：未知 configId 的结构块加载失败并返回可读 reason。
- [x] 6.3 Unity TestFramework EditMode：选择集导出结构块时使用 anchor 生成相对坐标。
- [x] 6.4 Unity TestFramework EditMode：结构块实例化到目标 anchor 时生成正确绝对坐标。
- [x] 6.5 Unity TestFramework EditMode：批量移动只生成调试请求，不直接修改 `ClientMapWorld`。
- [x] 6.6 Unity TestFramework EditMode：复制结构保留相对布局、方向和 port mask。
- [x] 6.7 Unity TestFramework EditMode：已删除实体从批量操作中跳过并记录失败 reason。
- [x] 6.8 Unity TestFramework EditMode：port 可视化读取最终 port mask，而不是只按 configId 推断。
- [x] 6.9 Unity TestFramework EditMode：runtime port effect 改变端口后，可视化数据反映最终端口。

## 7. Validation

- [x] 7.1 运行 `openspec validate add-debug-layout-tooling --strict --no-interactive`。
- [x] 7.2 运行相关 Unity TestFramework EditMode 测试。
- [x] 7.3 不执行 Unity Player build。
- [x] 7.4 用户手动 Play Mode：启动服务端和两个客户端，A 保存一组 port 连体结构为结构块，再加载复制到新位置，B 应看到服务端同步后的新结构。
- [x] 7.5 用户手动 Play Mode：A 框选多个实体并批量移动，B 应看到相同 entity id 的坐标变化；若任一请求失败，A 应显示失败 reason，B 不应出现客户端本地伪状态。
- [x] 7.6 用户手动 Play Mode：A 给实体添加 runtime port effect 后，调试可视化显示最终 port mask 和连接变化，B 看到一致结果。
