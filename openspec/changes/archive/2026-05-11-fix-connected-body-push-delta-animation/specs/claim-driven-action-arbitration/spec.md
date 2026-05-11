## MODIFIED Requirements
### Requirement: Connected Body Member Commit Boundary
系统 SHALL limit multi-member movement commits to an accepted connected body subject. Multi-member commit MUST NOT be used to model ordinary push chains, parent continuation, downstream blocker movement, or unrelated entity side effects. A push contact batch MAY relate multiple action units for multi-contact push propagation and result aggregation, but it MUST NOT turn those units into one combined member commit. When a push handoff resolves the downstream subject as a connected body, that downstream action unit MUST preserve the full resolved connected body subject through deferred action, pending action unit, accepted claims, planning, and commit.

#### Scenario: connected body 全体成功移动
- **WHEN** 一个 accepted connected body subject 包含多个 members
- **AND** every member source coordinate still matches
- **AND** every member target coordinate is valid or occupied only by another member's old body cell
- **THEN** commit applies all member position changes
- **AND** action result belongs to the connected body subject rather than to unrelated actions

#### Scenario: connected body 任一 member 阻塞
- **WHEN** 一个 accepted connected body subject 的任一 member target 被 external blocking entity 阻塞
- **THEN** arbitration or commit rejects the connected body action or derives blocker action units according to blocked policy and push contact batch rules
- **AND** no member position changes are applied until the connected body action later continues and succeeds

#### Scenario: 普通 push chain 不使用 member commit
- **WHEN** A pushes B and B may push C
- **THEN** A、B、C are represented as separate action units connected by handoff results or push contact batches
- **AND** commit MUST NOT treat A、B、C as members of one ordinary push action-unit transaction
- **AND** A or B MUST NOT parent retry and move later solely because a downstream subject moved

#### Scenario: body-to-body push chain 不扩展 parent member commit
- **WHEN** connected body A-B pushes connected body C-D
- **THEN** A-B 的 action unit 只提交 A-B 的 member changes
- **AND** C-D 的 movement 必须由独立 derived action unit 提交
- **AND** A-B does not commit C-D as its own side effect

#### Scenario: 中间 connected body 只传递 push
- **WHEN** connected body A-B receives push and its own movement is blocked by downstream subject C
- **THEN** A-B MUST derive or enqueue downstream push according to blocked policy
- **AND** A-B MUST NOT be re-enqueued as a parent retry movement action
- **AND** A-B members MAY receive push feedback metadata without any A-B member position commit
- **AND** only the terminal downstream subject that has valid target cells may produce movement commits

#### Scenario: push contact batch 聚合结果不做合并 member commit
- **WHEN** one parent action unit derives two child action units in the same push contact batch
- **THEN** batch result aggregation MAY decide parent continuation, batch failure, or owner result timing
- **AND** batch aggregation MUST NOT add any child subject member to the parent unit's commit member set

#### Scenario: handoff 推动连接体保留 downstream subject
- **WHEN** a player move, mechanism push, or configured push handoff is blocked by an entry member of a port-connected body
- **AND** the resolved handoff subject policy is `ConnectedBodyIfAny`
- **THEN** the derived downstream action unit MUST carry all members of the resolved connected body subject
- **AND** planning MUST produce member move claims for every member of that downstream subject
- **AND** commit MUST move the downstream subject all-or-nothing as one connected body action unit
