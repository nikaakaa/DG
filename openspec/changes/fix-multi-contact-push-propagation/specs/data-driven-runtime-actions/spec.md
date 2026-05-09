## MODIFIED Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置、运行时行为输入和运行时行为单元分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划、提交策略、handoff policy、handoff subject policy 和默认 cost 来源；`ActionRequest` SHALL 只携带一次运行时输入的 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params；action unit state SHALL 保存 ready tick、lifecycle、parent / derived 关系、pending 结果和 push contact batch 归属。运行时行为输入 MUST NOT 复制 `ActionSpec` 的静态策略，也 MUST NOT 承担 pending 生命周期状态或 push contact batch 状态。

#### Scenario: 运行时请求引用配置
- **WHEN** player move, auto move, mechanism push, debug move, debug spawn, or debug remove is created
- **THEN** the runtime input references an `ActionSpec`
- **AND** the runtime input carries only request-specific source, target, direction, tick, client tick, source state, and runtime params
- **AND** action unit state carries ready tick, lifecycle, parent / derived ids, pending status, and push contact batch id when applicable
- **AND** runtime input does not copy `ActionSpec` claim, conflict, interrupt, merge, plan, commit, handoff, or default cost policy

#### Scenario: 修改普通行为策略
- **WHEN** a move-like behavior changes required component, blocked component, priority, merge policy, interrupt policy, blocked policy, or default cost
- **THEN** the change is made in the `ActionSpec` source or registry
- **AND** core execution does not add a new branch for the behavior name

### Requirement: 执行层只消费统一结果
系统 SHALL keep execution focused on ready action units, accepted claims, plans, commit proposals, commit results, dirty state, pending state transitions, push contact batch transitions, and owner action results. Execution MUST NOT branch on ordinary business behavior names such as player move, auto move, mechanism push, wind push, trap pull, or ice slide.

#### Scenario: 执行统一 Move
- **WHEN** player move, auto move, mechanism push, or configured wind push reaches its ready tick
- **THEN** execution sends the ready action unit through the unified action pipeline
- **AND** execution does not switch by behavior name to decide movement rules

#### Scenario: 调试行为使用配置
- **WHEN** debug move, debug spawn, or debug remove is submitted
- **THEN** they also enter through their corresponding `ActionSpec`
- **AND** debug-specific permission and target policy are expressed as action data or explicit debug source policy

#### Scenario: Pending continuation uses unified execution
- **WHEN** a waiting parent action unit becomes ready to continue
- **THEN** execution treats it like another ready action unit
- **AND** it is not advanced through a push-front special branch

#### Scenario: push contact batch uses unified execution
- **WHEN** one blocked action unit creates a push contact batch with multiple child units
- **THEN** execution schedules those child units through the same ready action unit path
- **AND** execution does not add a behavior-name branch to move all child subjects directly

#### Scenario: push-specific handoff interpretation stays out of execution
- **WHEN** a blocked move discovers multiple external push contacts
- **THEN** contact-to-handoff-subject interpretation happens in the action policy / arbitration layer
- **AND** execution only observes derived action units, pending transitions, commit results, and owner action results
- **AND** execution does not infer child unit count from raw contact count

## ADDED Requirements
### Requirement: Push Contact Batch State Separation
系统 SHALL store push contact batch state separately from `ActionRequest` and `ActionSpec`. Push contact batch state MUST be runtime lifecycle data that relates action units created by the same blocked step. Static action policy data MAY select blocked and handoff behavior, but it MUST NOT store per-instance child unit ids, batch completion, or owner result state.

#### Scenario: batch state 不进入 ActionSpec
- **WHEN** a blocked action creates a push contact batch
- **THEN** the batch id, child unit ids, batch status, and completion result are stored in pending state
- **AND** the referenced `ActionSpec` remains static policy data

#### Scenario: runtime request 不复制 batch state
- **WHEN** a child action unit is converted into an `ActionRequest`
- **THEN** the request carries only source state, derived id, owner id, spec id, target, direction, tick, and runtime params needed for arbitration
- **AND** the push contact batch lifecycle remains in pending state

#### Scenario: batch state does not imply propagation length policy
- **WHEN** a push contact batch creates or observes multiple derived action units
- **THEN** pending state records batch lifecycle and child unit ids
- **AND** it does not introduce a propagation-length failure policy
- **AND** raw contact count and subject member count do not become behavior limits

### Requirement: Core Extension Boundary For Multi-Contact Push
系统 SHALL treat multi-contact push propagation as a focused implementation-layer extension to the push pipeline, not as a complete transaction system or ordinary behavior config tweak. Adding multi-contact push support MUST include focused tests for existing single-child handoff compatibility, contact set discovery, multi-child push batch creation, batch success/failure aggregation, and data-driven behavior-name independence.

#### Scenario: 新增 multi-contact push 能力
- **WHEN** the system adds support for one parent action unit deriving multiple child action units in one blocked step
- **THEN** the change is implemented as focused push contact collection and pending batch support
- **AND** ordinary action names do not select batch behavior through string matching

#### Scenario: 普通行为继续数据驱动
- **WHEN** a new ordinary move-like behavior uses existing multi-contact push, blocked policy, and handoff policy support
- **THEN** it can be added through action policy data and tests
- **AND** no new core branch is required unless it introduces a new primitive or reusable policy type
