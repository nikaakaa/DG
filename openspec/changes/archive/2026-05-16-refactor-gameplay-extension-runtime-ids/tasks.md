## 1. 现状审计
- [x] 1.1 列出 `ActionPrimitive`、`CommitProposalKind`、`ComponentKind`、`ComponentResultKind`、`EffectKind`、`RuntimeEffectKind`、`ActionTargetRule` 在 Shared GameCore、Luban 定义、测试中的引用。
- [x] 1.2 标记这些 enum 中哪些仍是兼容/内部状态，哪些不能再作为正式玩法扩展入口。
- [x] 1.3 增加自动扫描测试，防止正式 Luban schema / 生成 JSON / StreamingAssets 回流到 action primitive、target rule、component kind、effect kind authoring 字段。
- [x] 1.4 保留旧 enum 仅作为兼容测试和现有 helper 输入，新增注册路径改为 id-first。

## 2. Action strategy id 收口
- [x] 2.1 调整 `IActionStrategy` 和注册 metadata，使 strategy id 成为主键，正式路径不再用 `ActionPrimitive` 选择策略。
- [x] 2.2 调整 `ActionSpec` 和 Luban provider，正式策略选择只消费 `strategy_id`。
- [x] 2.3 迁移 move、spawn、remove、runtime effect 策略到 id-first 注册。
- [x] 2.4 保留测试专用 strategy，验证新增 strategy 不需要修改 `ActionPrimitive`、`StateDrivenRuleExecutionSystem` 或中心分支。

## 3. Target selector id 收口
- [x] 3.1 从正式 Luban/action authoring JSON 路径移除 `ActionTargetRule` 字段。
- [x] 3.2 让正式 `ActionSpec` 目标选择引用 targeting policy id 和 selector id。
- [x] 3.3 self、direction cell、target coord、front entities selector 已走 selector id 注册。
- [x] 3.4 EditMode 测试覆盖新增 selector 不修改中心 selector switch。

## 4. Commit handler id 收口
- [x] 4.1 增加 `CommitProposalId`，替代 `CommitProposalKind` 作为 handler registry 主键。
- [x] 4.2 `CommitProposal` 保留兼容 kind 字段，同时新增 id-first proposal id。
- [x] 4.3 迁移内置 commit handler 到 id-first 注册。
- [x] 4.4 测试专用 commit handler 通过 id 注册，验证 `CommitResolver` 不需要新增分支。
- [x] 4.5 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore` 已验证 move group、occupied target、priority 语义未回归。

## 5. Component applicator 与 fact query id 收口
- [x] 5.1 `ComponentApplicationRegistry` 正式 API 支持 `ComponentId`，普通应用路径走 id。
- [x] 5.2 `EntityArchetype` 正式组件列表支持 component id，正式 JSON 改为 `component_ids`。
- [x] 5.3 内置静态组件 applicator 通过 component id 注册。
- [x] 5.4 target filter / blocked condition 已有 component fact query registry，测试覆盖未知 id 明确失败。
- [x] 5.5 测试专用 component applicator 和 fact query 验证新增 component 不需要修改 `ComponentKind`。

## 6. Runtime effect payload id 收口
- [x] 6.1 增加 `EffectPayloadId`，作为 effect payload 选择键。
- [x] 6.2 `EffectSpec` 与 Luban provider 正式 payload 选择改为 `effect_payload_id`。
- [x] 6.3 `RuntimeEffectSpec` 保存 payload id，同时保留 legacy kind 兼容读法。
- [x] 6.4 blocking、auto move、pushable、port connector、movement permission、tag payload 迁移到内置 payload id。
- [x] 6.5 `ComponentStateResolver` 主流程使用 payload id 判断 runtime effect contribution。
- [x] 6.6 EditMode 测试覆盖 runtime effect payload 通过 id 进入 final component result。

## 7. Component result id 收口
- [x] 7.1 `ComponentSourceContribution` 和 resolver registry 以 `ComponentResultId` 为主键。
- [x] 7.2 Blocking、AutoMove、Pushable、PortConnector、MovementPermission resolver 迁移到 id-first。
- [x] 7.3 测试专用 component result resolver 验证新增 final result 不需要修改 `ComponentResultKind`。

## 8. Snapshot / Delta payload id 收口
- [x] 8.1 增加 `SnapshotPayloadId`，projector / applier 暴露 payload id。
- [x] 8.2 现有同步组件 projector / applier 迁移到 payload id 注册。
- [x] 8.3 测试专用 snapshot payload 验证新增同步 payload 不需要修改中心 snapshot/delta 分支。

## 9. 配置与生成
- [x] 9.1 更新 Luban 定义，使用 `strategy_id`、`selector_id`、`component_ids`、`effect_payload_id` 替代正式扩展 enum authoring 字段。
- [ ] 9.2 运行 Luban 导出并更新正式生成 C# / JSON；本轮手动同步了当前实际编译路径和正式 JSON，但没有用 Luban 工具重导 `.xlsx` 源表。
- [x] 9.3 迁移正式生成 JSON 和 Unity StreamingAssets 配置数据到 id-first 字段。
- [x] 9.4 从正式 Luban schema、正式生成 JSON、StreamingAssets 中删除 `ActionPrimitive`、`ActionTargetRule`、`ComponentKind`、`EffectKind` 扩展 enum authoring 字段。
- [ ] 9.5 fallback provider 普通构造路径仍需继续清理为完全 id-first；本轮保留部分 legacy helper 作为兼容入口。

## 10. 自动验证
- [x] 10.1 运行 `openspec validate refactor-gameplay-extension-runtime-ids --strict --no-interactive`。
- [x] 10.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 10.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 10.4 运行 Unity TestFramework EditMode，覆盖 action strategy、target selector、commit handler、component applicator、component result resolver、effect payload、snapshot payload id-first 扩展。
- [x] 10.5 相关 EditMode 回归通过：`DataDrivenRuntimeActionTests`、`ComponentSystemWorkflowTests`、`RuntimeComponentResultTests`、`PushOnEnterTileTests` 共 136/136。

## 11. 手动端到端验证
- [ ] 11.1 用户启动 Fantasy 服务端。
- [ ] 11.2 用户启动两个 Unity Play Mode 客户端并 JoinWorld。
- [ ] 11.3 用户验证玩家移动、自动移动、机关推动仍通过服务端 WorldDelta 同步。
- [ ] 11.4 用户验证新增 id-first 行为和 effect payload 在两个客户端结果一致。
- [ ] 11.5 用户验证客户端不通过本地 action/effect enum 裁决权威结果。
