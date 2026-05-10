## 1. 配置数据边界

- [x] 1.1 用 Python 编辑 `Config/Luban/Defines/gamecore.xml`，新增 `PushOnEnterConfig` bean 和 `TbPushOnEnterConfig` 表。
- [x] 1.2 用 Python 新建或更新 `Config/Luban/Datas/gamecore/push_on_enter_config.xlsx`，字段包含 `config_id`、`output_spec_id`、`output_cost_ticks`。
- [x] 1.3 给所有包含 `ComponentKind.PushOnEnter` 的正式 archetype 填写 PushOnEnter 配置。
- [x] 1.4 删除旧 fallback provider 数据，或把 fallback provider 从正式测试路径移除。
- [x] 1.5 运行 `Config/Luban/Run.ps1` 生成 Shared 表代码、JSON 和 Unity StreamingAssets 数据。
- [x] 1.6 添加配置缺失、spec id 无效、cost 非法时的明确失败。

## 2. 组件应用清理

- [x] 2.1 修改 `IGameConfigProvider` / `LubanGameConfigProvider`，提供按 `config_id` 查询 PushOnEnter 输出配置的接口。
- [x] 2.2 修改 `PushOnEnterComponent` 构造来源，使其读取 Luban 输出配置数据。
- [x] 2.3 移除 `ComponentApplicationRegistry` 中 `"mechanism_push"` 硬编码。
- [x] 2.4 验证 conveyor 仍由通用 `PushOnEnterComponent` 组合表达。
- [x] 2.5 添加测试覆盖不同 PushOnEnter output spec 的实体能产生不同行为。

## 3. Pending 主链路隔离

- [x] 3.1 给 `StateDrivenRuleExecutionSystem` 增加不需要 `PendingRuleStateStore` 的主入口。
- [x] 3.2 将 server authoritative tick runner 迁移到无 pending 主入口。
- [x] 3.3 将 sandbox local runner 迁移到无 pending 主入口。
- [x] 3.4 从 `ActionArbiter.ArbitrateMoves` 的普通 move/deferred 路径移除 pending 参数。
- [x] 3.5 从 `ActionSpecs` push/deferred 分支移除 pending 参数和 `FindActionState` 残留引用。
- [x] 3.6 保留或移动 pending legacy API 到隔离测试边界，并标明不是 push propagation 主路径。

## 4. 行为回归测试

- [x] 4.1 Unity EditMode 覆盖 PushOnEnter 使用配置 output spec。
- [x] 4.2 Unity EditMode 覆盖 mechanism push、configured wind push、deferred push output 行为不变。
- [x] 4.3 Unity EditMode 覆盖 multi-contact same downstream subject 去重不依赖 pending。
- [x] 4.4 Unity EditMode 覆盖 sandbox runner 消费 deferred output。
- [x] 4.5 Server authoritative verification 覆盖 source action 不等待 downstream deferred result。

## 5. 验证

- [x] 5.1 运行 `openspec validate refactor-action-output-cleanup --strict --no-interactive`。
- [x] 5.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 5.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 5.4 运行 Unity TestFramework EditMode 相关测试。
- [x] 5.5 用户手动 Play Mode 验证 conveyor / closed-loop push device 不挂死。
- [x] 5.6 用户手动双客户端验证 WorldDelta 序列一致。
