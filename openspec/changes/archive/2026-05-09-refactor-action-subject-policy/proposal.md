# Change: 收紧 ActionSpec subject policy

## Why
当前系统已经用 `ActionSpecId` 查找行为配置，但 connected body subject 仍容易被理解成某个特殊 action 名的能力。这样会让规则层继续围绕 action 名字扩展分支，而不是读取 `ActionSpec` 字段。

本 change 要把“动作命中入口 entity 后，本次结算作用于谁”收敛为 `ActionSpec` 的 subject policy。`mechanism_push` 只是现有配置中的一个验证样例，不是规则层可硬编码判断的特殊名字。

## What Changes
- 将 `ActionSpec` 中现有 subject kind 收紧为明确的 subject policy 语义。
- 明确 `ActionSpecId` 只用于查配置，规则层不得根据 `mechanism_push`、`connected_body_move` 等名字分支决定 subject。
- 让需要 connected body 结算的现有行为配置通过 `ActionSpec.subjectPolicy` 选择 connected body subject。
- 保留 `connected_body_move` 作为兼容过渡，不再把它作为新增动作来源的默认模式。
- 明确第一阶段不实现 `CompositeEntity`，也不引入 `CompositeEntityIfAny` 运行时代码。
- 补齐 Unity TestFramework EditMode 和现有 Shared/server 验证要求，端到端同步由用户手动验证。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `claim-driven-action-arbitration`
- Affected code: `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`, `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`, `Client/DG_Client/Assets/Tests/Editor/ClientWorld/*`
- Related docs: `docs/Goal/subject-and-composite-entity.md`, `openspec/project.md`
- Related active changes: `refactor-port-linked-body-foundation`, `refactor-body-push-chain-planning`
