## ADDED Requirements
### Requirement: Action Subject Boundary
系统 SHALL distinguish the subject of an action unit from the raw entity that requested it. An action subject MUST be either a single entity subject or a connected body subject. Ordinary push handoff MUST NOT expand the source action subject into a multi-entity transaction.

#### Scenario: 普通移动使用单 entity subject
- **WHEN** 一个没有 port connected body 的 entity 发起普通 move action
- **THEN** 仲裁层将该 action 视为 single entity subject
- **AND** accepted planning 只为该 source entity 生成提交结果

#### Scenario: 普通 push 不扩展 source subject
- **WHEN** A 的普通 move 命中可推动的 B
- **THEN** A 的 action result 是 handoff
- **AND** B 通过独立 action unit 裁决自己的移动
- **AND** A 的 source action 不把 B 加入自己的原子提交范围

#### Scenario: connected body 使用 connected body subject
- **WHEN** `PortConnectionSystem` 或后续 PortGraph 将 A、B、C 解析为同一 connected body view
- **THEN** A 发起的 connected body move action MAY use the connected body view as its action subject
- **AND** 该 action 对外只有一个 action result
- **AND** 该 action 内部 MAY produce member move commits for A、B、C

### Requirement: Connected Body Member Commit Boundary
系统 SHALL limit multi-member movement commits to an accepted connected body subject. Multi-member commit MUST NOT be used to model ordinary push chains, parent retry, or unrelated entity side effects.

#### Scenario: connected body 全体成功移动
- **WHEN** 一个 accepted connected body subject 包含多个 members
- **AND** every member source coordinate still matches
- **AND** every member target coordinate is valid or occupied only by another member's old body cell
- **THEN** commit applies all member position changes
- **AND** action result belongs to the connected body subject rather than to unrelated actions

#### Scenario: connected body 任一 member 阻塞
- **WHEN** 一个 accepted connected body subject 的任一 member target 被 external blocking entity 阻塞
- **THEN** commit rejects the connected body action
- **AND** no member position changes are applied

#### Scenario: 普通 push chain 不使用 member commit
- **WHEN** A pushes B and B may push C
- **THEN** A、B、C are represented as separate action units connected by handoff results
- **AND** commit MUST NOT treat A、B、C as members of one ordinary push transaction
