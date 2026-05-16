# Change: 收口玩法扩展运行时 ID 边界

## Why
当前动作、提交、组件、运行时效果和目标选择已经开始注册表化，但部分扩展点仍保留 `ActionPrimitive`、`CommitProposalKind`、`ComponentKind`、`ComponentResultKind`、`EffectKind`、`RuntimeEffectKind`、`ActionTargetRule` 等 enum 作为新增能力入口。商业化玩法扩展会频繁增加行为、效果、组件结果和提交语义，继续依赖 enum 会让新增能力反复修改核心文件和 Luban 生成枚举。

## What Changes
- **BREAKING**: 新增玩法扩展能力不得通过新增扩展 enum 成员完成，必须使用稳定配表 id / runtime id 和显式注册模块。
- 将 action strategy、target selector、commit handler、component applicator、component result resolver、effect payload mapper、snapshot payload 等扩展点统一收口为 id 驱动。
- 正式 Luban 源表、正式生成配置、fallback provider 普通路径和新增测试不再使用可扩展玩法 enum；旧 enum 只允许作为兼容专项测试或稳定内部状态存在。
- 增加防回流测试，证明新增策略、目标选择、commit、effect payload、component result 不需要修改中心 enum 或中心 switch。
- 明确迁移顺序，在本变更内清理正式配置中的扩展 enum authoring 字段，迁移规则层和测试入口到 id-first。

## Impact
- Affected specs:
  - `data-driven-runtime-actions`
  - `runtime-component-results`
  - `shared-gamecore-entity-rules`
- Affected code:
  - `Shared/DG.GameCore/ActionRuntime/Specs/*`
  - `Shared/DG.GameCore/ActionRuntime/Strategies/*`
  - `Shared/DG.GameCore/ActionRuntime/Targeting/*`
  - `Shared/DG.GameCore/ActionRuntime/Commit/*`
  - `Shared/DG.GameCore/Configuration/*`
  - `Shared/DG.GameCore/RuntimeEffects/*`
  - `Shared/DG.GameCore/Snapshots/*`
  - `Config/Luban/Defines/gamecore.xml`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor`

## Non-Goals
- 不引入完整技能系统或 UE GAS 对象模型。
- 不改 Fantasy 网络结构、Unity 表现层职责或 WorldDelta 协议语义。
- 不引入运行时反射扫描来选择玩法能力；注册必须显式、确定、可测试。
- 不把 `Direction`、生命周期状态、固定错误码、持续时间策略、堆叠策略等明确稳定、以后不会按玩法扩展的大类强行改成配表 id。

## Validation
- `openspec validate refactor-gameplay-extension-runtime-ids --strict --no-interactive`
- `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode 覆盖新增 id 驱动 strategy、target selector、commit handler、effect payload mapper、component result resolver、component applicator 和 snapshot payload projector。
- 用户手动端到端验证：启动 Fantasy 服务端和两个 Unity Play Mode 客户端，验证现有玩家移动、自动移动、机关推动、runtime effect、临时端口和新增测试行为全部由服务端结果同步，客户端不本地裁决。
