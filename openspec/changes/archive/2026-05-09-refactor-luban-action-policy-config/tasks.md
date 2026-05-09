## 1. Luban 数据源
- [x] 1.1 在 `gamecore.xml` 增加 action policy enum：primitive。
- [x] 1.2 在 `gamecore.xml` 增加 action policy enum：source。
- [x] 1.3 在 `gamecore.xml` 增加 action policy enum：priority。
- [x] 1.4 在 `gamecore.xml` 增加 action policy enum：target rule。
- [x] 1.5 在 `gamecore.xml` 增加 action policy enum：blocked policy。
- [x] 1.6 在 `gamecore.xml` 增加 action policy enum：handoff policy。
- [x] 1.7 在 `gamecore.xml` 增加 action policy enum：conflict policy。
- [x] 1.8 在 `gamecore.xml` 增加 action policy enum：interrupt policy。
- [x] 1.9 在 `gamecore.xml` 增加 action policy enum：merge policy。
- [x] 1.10 在 `gamecore.xml` 增加 action policy enum：subject policy。
- [x] 1.11 在 `gamecore.xml` 增加 action policy enum：plan rule。
- [x] 1.12 在 `gamecore.xml` 增加 action policy enum：commit rule。
- [x] 1.13 增加 action policy bean，覆盖当前 `ActionSpec` 所有静态字段。
- [x] 1.14 增加 handoff policy 字段或 bean，表达派生 spec、派生 subject policy 和链式安全输入。
- [x] 1.15 增加 `action_spec.xlsx`。
- [x] 1.16 在 `action_spec.xlsx` 录入 `player_move` 当前行为。
- [x] 1.17 在 `action_spec.xlsx` 录入 `auto_move` 当前行为。
- [x] 1.18 在 `action_spec.xlsx` 录入 `mechanism_push` 当前行为。
- [x] 1.19 在 `action_spec.xlsx` 录入 `debug_move` 当前行为。
- [x] 1.20 在 `action_spec.xlsx` 录入 `debug_spawn` 当前行为。
- [x] 1.21 在 `action_spec.xlsx` 录入 `debug_remove` 当前行为。
- [x] 1.22 在 `action_spec.xlsx` 录入 `player_push` 当前行为。
- [x] 1.23 在 `action_spec.xlsx` 录入 `configured_wind_push` 当前行为。
- [x] 1.24 在 `action_spec.xlsx` 录入 `connected_body_move` 当前行为。
- [x] 1.25 运行 Luban 导出，确认 JSON 生成。
- [x] 1.26 运行 Luban 导出，确认 C# provider 生成。

## 2. Shared GameCore 映射
- [x] 2.1 增加 Luban DTO 到 `ActionSpec` 的 mapper。
- [x] 2.2 增加 Luban-backed `ActionSpecRegistry` 构建路径。
- [x] 2.3 让正式 server/client 入口优先注入 Luban-backed registry。
- [x] 2.4 删除正式运行时内置 fallback action list。
- [x] 2.5 确保 `ActionSpecRegistry.Default` 读取 Luban 生成数据。
- [x] 2.6 给未知 enum 增加显式失败。
- [x] 2.7 给未知 spec 增加显式失败。
- [x] 2.8 给重复 spec id 增加显式失败。
- [x] 2.9 增加 parity 测试，验证 Luban `player_move` 与当前期望一致。
- [x] 2.10 增加 parity 测试，验证 Luban `player_push` 与当前期望一致。
- [x] 2.11 增加 parity 测试，验证 Luban `mechanism_push` 与当前期望一致。
- [x] 2.12 增加 parity 测试，验证 Luban `auto_move` 与当前期望一致。

## 3. Handoff 去硬编码
- [x] 3.1 给 `ActionSpec` 增加显式 handoff policy 数据。
- [x] 3.2 让 handoff policy 支持派生 spec id。
- [x] 3.3 让 handoff policy 支持派生 subject policy。
- [x] 3.4 让 handoff policy 支持链式安全输入。
- [x] 3.5 移除 `ResolveHandoffSubject()` 中的 `"player_push"` 字符串选择。
- [x] 3.6 移除 `ResolveHandoffSubject()` 中的 `"connected_body_move"` 字符串选择。
- [x] 3.7 让 pending child unit 的 spec id 来自 handoff policy。
- [x] 3.8 移除 `PendingRuleStateStore.AddHandoffActionState()` 的默认 `"player_push"`。
- [x] 3.9 增加测试：同一个 blocker 通过不同 handoff policy 派生不同 spec。
- [x] 3.10 增加测试：connected body handoff 的 subject 来自 policy，不来自 action 名。

## 4. Legacy 入口删除
- [x] 4.1 让 `EnqueueConfiguredMove()` 不再伪装成 `WorldActionKind.MechanismPush`。
- [x] 4.2 删除 `WorldActionKind` 运行时分类。
- [x] 4.3 增加测试：新增 action spec 不需要新增 `WorldActionKind`。
- [x] 4.4 检查 server 正式入口都传递或解析 `ActionSpecId`。
- [x] 4.5 检查 client 正式入口都传递或解析 `ActionSpecId`。
- [x] 4.6 检查 pending retry 不依赖 legacy kind 表达行为。

## 5. 第一层停止线
- [x] 5.1 增加测试：新增普通 move-like 行为只改配置和测试，不改核心规则模块。
- [x] 5.2 增加测试：修改 handoff 派生 spec 只改配置，不改 resolver。
- [x] 5.3 增加测试：规则层不按 `ActionSpecId` 字符串选择 subject。
- [x] 5.4 增加测试：规则层不按 `ActionSpecId` 字符串选择 handoff。
- [x] 5.5 在文档中标记本 change 完成后转入 runtime final component state 阶段。

## 6. 验证
- [x] 6.1 运行 `openspec validate refactor-luban-action-policy-config --strict --no-interactive`。
- [x] 6.2 运行 Luban 导出脚本。
- [x] 6.3 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 6.4 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 6.5 运行 Unity TestFramework EditMode，覆盖 action policy 加载。
- [x] 6.6 运行 Unity TestFramework EditMode，覆盖 handoff policy。
- [x] 6.7 运行 Unity TestFramework EditMode，覆盖 legacy 删除。
- [x] 6.8 运行 Unity TestFramework EditMode，覆盖 connected body push。
- [ ] 6.9 用户手动 Play Mode 验证玩家移动。
- [ ] 6.10 用户手动 Play Mode 验证 push。
- [ ] 6.11 用户手动 Play Mode 验证玩家作为 pushable entry。
- [ ] 6.12 用户手动 Play Mode 验证 port connected body push。
- [ ] 6.13 用户手动 Play Mode 验证机制推动。
- [ ] 6.14 用户手动双客户端验证 WorldDelta 同步。
