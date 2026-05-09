## MODIFIED Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置、运行时行为输入和运行时行为单元分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划、提交策略和默认 cost 来源；`ActionRequest` SHALL 只携带一次运行时输入的 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params；action unit state SHALL 保存 ready tick、lifecycle、parent / derived 关系、retry 和 pending 结果。运行时行为输入 MUST NOT 复制 `ActionSpec` 的静态策略，也 MUST NOT 承担 pending 生命周期状态。

#### Scenario: 运行时请求引用配置
- **WHEN** player move, auto move, mechanism push, debug move, debug spawn, or debug remove is created
- **THEN** the runtime input references an `ActionSpec`
- **AND** the runtime input carries only request-specific source, target, direction, tick, client tick, source state, and runtime params
- **AND** action unit state carries ready tick, lifecycle, parent / derived ids, retry count, and pending status
- **AND** runtime input does not copy `ActionSpec` claim, conflict, interrupt, merge, plan, commit, or default cost policy

#### Scenario: 修改普通行为策略
- **WHEN** a move-like behavior changes required component, blocked component, priority, merge policy, interrupt policy, blocked policy, or default cost
- **THEN** the change is made in the `ActionSpec` source or registry
- **AND** core execution does not add a new branch for the behavior name

### Requirement: 数据驱动仲裁 claim
系统 SHALL 在仲裁层从 ready action unit、`ActionRequest` 输入和 `ActionSpec` 生成 `ActionClaim`。仲裁 MUST use claims to decide body conflict, target occupancy, same-claim merge, exclusive resources, and derived action proposals instead of hardcoding per behavior-name conflict branches.

#### Scenario: 目标格独占
- **WHEN** two ready action units claim the same exclusive target cell
- **THEN** arbitration / conflict policy decides which unit may proceed
- **AND** the decision uses claim data instead of behavior-name branches

#### Scenario: 同 body 合并
- **WHEN** two ready action units from the same connected body request the same body move claim
- **THEN** merge policy may collapse them into a single accepted unit result
- **AND** no duplicate body movement is committed

#### Scenario: 同 body 冲突
- **WHEN** two ready action units from the same connected body request incompatible move claims
- **THEN** conflict policy rejects, interrupts, or fails the lower priority unit
- **AND** no partial body movement is committed

#### Scenario: 阻挡派生行为
- **WHEN** a ready action unit is blocked by a pushable blocker
- **THEN** arbitration may emit a derived action unit proposal
- **AND** the parent action unit enters waiting state instead of reporting success

### Requirement: 执行层只消费统一结果
系统 SHALL keep execution focused on ready action units, accepted claims, plans, commit proposals, commit results, dirty state, pending state transitions, and owner action results. Execution MUST NOT branch on ordinary business behavior names such as player move, auto move, mechanism push, wind push, trap pull, or ice slide.

#### Scenario: 执行统一 Move
- **WHEN** player move, auto move, mechanism push, or configured wind push reaches its ready tick
- **THEN** execution sends the ready action unit through the unified action pipeline
- **AND** execution does not switch by behavior name to decide movement rules

#### Scenario: 调试行为使用配置
- **WHEN** debug move, debug spawn, or debug remove is submitted
- **THEN** they also enter through their corresponding `ActionSpec`
- **AND** debug-specific permission and target policy are expressed as action data or explicit debug source policy

#### Scenario: Pending retry uses unified execution
- **WHEN** a waiting parent action unit becomes ready to retry
- **THEN** execution treats it like another ready action unit
- **AND** it is not advanced through a push-front special branch
