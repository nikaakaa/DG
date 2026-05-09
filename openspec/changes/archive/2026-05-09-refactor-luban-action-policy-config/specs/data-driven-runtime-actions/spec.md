## MODIFIED Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置、运行时行为输入和运行时行为单元分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划、提交策略、handoff 策略、subject 策略和默认 cost 来源；正式 `ActionSpec` 数据 SHALL come from Luban Excel configuration through a provider/mapper; `ActionRequest` SHALL 只携带一次运行时输入的 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params；action unit state SHALL 保存 ready tick、lifecycle、parent / derived 关系、retry 和 pending 结果。运行时行为输入 MUST NOT 复制 `ActionSpec` 的静态策略，也 MUST NOT 承担 pending 生命周期状态。

#### Scenario: 运行时请求引用配置
- **WHEN** player move, auto move, mechanism push, debug move, debug spawn, or debug remove is created
- **THEN** the runtime input references an `ActionSpec`
- **AND** the runtime input carries only request-specific source, target, direction, tick, client tick, source state, and runtime params
- **AND** action unit state carries ready tick, lifecycle, parent / derived ids, retry count, and pending status
- **AND** runtime input does not copy `ActionSpec` claim, conflict, interrupt, merge, plan, commit, subject, handoff, or default cost policy

#### Scenario: 修改普通行为策略
- **WHEN** a move-like behavior changes required component, blocked component, priority, merge policy, interrupt policy, blocked policy, handoff policy, subject policy, or default cost
- **THEN** the change is made in the Luban action policy Excel source or its generated registry data
- **AND** core execution does not add a new branch for the behavior name

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing primitives and policies to be added by Luban action policy data and tests. It MUST NOT require new branches in core execution, arbitration, planning, pending state, or commit orchestration.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing `Move` primitive、mechanism-like source、exclusive target cell claim 和 blocked tag policy
- **THEN** 开发者新增或修改 Luban Excel action policy row
- **AND** 运行 Luban 导出生成 JSON/provider
- **AND** 添加 Unity TestFramework EditMode 覆盖
- **AND** 不修改核心 execution / arbitration orchestration code

#### Scenario: 新增底层原语
- **WHEN** 新需求无法由已有 primitive 和 reusable policies 表达
- **THEN** 系统 MAY add a new primitive or policy type
- **AND** 该新增 MUST be treated as core extension with explicit tests and proposal/task coverage

## ADDED Requirements
### Requirement: WorldActionKind 删除边界
系统 SHALL remove `WorldActionKind` from the runtime action model. All ordinary behaviors MUST enter the runtime through `ActionSpecId` and action policy data. Queueing, pending, arbitration, planning, and commit APIs MUST NOT disguise a configured action as an unrelated legacy kind to obtain behavior.

#### Scenario: configured action 只携带 ActionSpecId
- **WHEN** a configured move-like action is enqueued by `ActionSpecId`
- **THEN** its primitive, source, priority, target rule, and handoff behavior are resolved from the referenced `ActionSpec`
- **AND** the runtime action does not carry `WorldActionKind`

#### Scenario: convenience enqueue methods fill spec id
- **WHEN** existing convenience methods create player move, auto move, mechanism push, debug move, debug spawn, or debug remove
- **THEN** they fill an explicit `ActionSpecId`
- **AND** the resulting request follows the same policy path as any configured action
