## MODIFIED Requirements
### Requirement: Action Subject Boundary
系统 SHALL distinguish the subject of an action unit from the raw entity that requested it. An action subject MUST be selected by the resolved `ActionSpec` subject policy and MUST be either a single entity subject or a connected body subject in this stage. Ordinary push handoff MUST NOT expand the source action subject into a multi-entity transaction, and `CompositeEntity` subject selection is deferred to a future change. The arbiter MUST NOT choose subject behavior by matching action names.

#### Scenario: 普通移动使用 single entity subject
- **WHEN** 一个 action 的 resolved subject policy 是 `HitEntity`
- **THEN** 仲裁层将该 action 视为 single entity subject
- **AND** accepted planning 只为该 subject 生成提交结果

#### Scenario: 普通 push 不扩展 source subject
- **WHEN** A 的普通 move 命中可推动的 B
- **THEN** A 的 action result 是 handoff
- **AND** B 通过独立 action unit 裁决自己的移动
- **AND** A 的 source action 不把 B 加入自己的原子提交范围

#### Scenario: policy 允许 connected body subject
- **WHEN** a move-like action enters arbitration with resolved subject policy `ConnectedBodyIfAny`
- **AND** its entry entity belongs to a port connected body
- **THEN** 仲裁层 MAY use the connected body view as the action subject
- **AND** 该 action 对外只有一个 action result
- **AND** 该 action 内部 MAY produce member move commits for all subject members

#### Scenario: connected body 兼容 action 保留
- **WHEN** an existing compatibility action such as `connected_body_move` enters arbitration during migration
- **THEN** it MAY still use connected body subject policy
- **AND** new movement sources do not need to copy that action name only to select connected body subject

#### Scenario: action 名不决定 subject
- **WHEN** an action id is renamed or a second action id is configured with the same subject policy
- **THEN** arbitration subject selection remains equivalent
- **AND** only explicit `ActionSpec` policy differences can change subject selection

## ADDED Requirements
### Requirement: Action Subject Policy Verification
系统 SHALL verify action subject policy behavior through Unity TestFramework EditMode tests and existing Shared/server validation. Unity Player build MUST NOT be required for automated validation, and end-to-end runtime sync MUST remain a manual Play Mode / two-client verification step.

#### Scenario: EditMode covers subject selection
- **WHEN** Unity TestFramework EditMode tests run
- **THEN** they cover BodyResolver resolving the same connected body from any member
- **AND** an action configured with `ConnectedBodyIfAny` moving a connected body as one subject
- **AND** an action configured with `ConnectedBodyIfAny` rejecting or waiting without partial member movement when any subject member is blocked
- **AND** ordinary non-port entity behavior remains unchanged

#### Scenario: EditMode proves policy not name
- **WHEN** two action specs have different ids but the same subject policy
- **THEN** tests verify subject selection follows the shared policy
- **AND** no test relies on action name matching to obtain connected body behavior

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** policy-driven connected body movement and handoff results converge to the same server final state on both clients
- **AND** the report distinguishes OpenSpec validation, automated tests, server verification, and actual two-client runtime observation
