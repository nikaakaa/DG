## MODIFIED Requirements
### Requirement: Pending Push Uses Unified Action Flow
系统 SHALL express pending push continuation as parent / derived action units in the same claim-driven arbiter. Pending push MUST NOT use execution-layer special branches for port front, chain expansion, final move acceptance, or same-tick whole-chain solving when those can be represented through reusable arbitration policies and action unit retry.

#### Scenario: Pending parent retries through claims
- **WHEN** a derived blocker action unit succeeds
- **THEN** the waiting parent action unit becomes ready to retry on a later ready tick
- **AND** the retried parent action unit enters the same claim-driven arbiter

#### Scenario: Pending completion waits for parent result
- **WHEN** a derived push action unit commits successfully
- **THEN** the owner action is not marked successful solely because the derived action succeeded
- **AND** the owner action succeeds only after the parent action unit retries and commits its own claims

#### Scenario: Pending push keeps chain safety
- **WHEN** an action unit retry or derivation would loop, exceed retry limits, exceed pending timeout, exceed chain depth, or duplicate an active child
- **THEN** the pending action unit boundary fails the unit with stable reason
- **AND** existing covered push outcomes remain compatible

#### Scenario: Body chain remains action-unit based
- **WHEN** connected body A is blocked by connected body B and B is blocked by connected body C
- **THEN** A, B, and C are represented as separate action units connected by handoff results
- **AND** execution does not create one combined A-B-C commit transaction

### Requirement: Connected Body Member Commit Boundary
系统 SHALL limit multi-member movement commits to an accepted connected body subject. Multi-member commit MUST NOT be used to model ordinary push chains, parent retry, downstream blocker movement, or unrelated entity side effects.

#### Scenario: connected body 全体成功移动
- **WHEN** 一个 accepted connected body subject 包含多个 members
- **AND** every member source coordinate still matches
- **AND** every member target coordinate is valid or occupied only by another member's old body cell
- **THEN** commit applies all member position changes
- **AND** action result belongs to the connected body subject rather than to unrelated actions

#### Scenario: connected body 任一 member 阻塞
- **WHEN** 一个 accepted connected body subject 的任一 member target 被 external blocking entity 阻塞
- **THEN** arbitration or commit rejects the connected body action or derives a blocker action according to blocked policy
- **AND** no member position changes are applied until the connected body action later retries and succeeds

#### Scenario: 普通 push chain 不使用 member commit
- **WHEN** A pushes B and B may push C
- **THEN** A、B、C are represented as separate action units connected by handoff results
- **AND** commit MUST NOT treat A、B、C as members of one ordinary push transaction

#### Scenario: body-to-body push chain 不扩展 member commit
- **WHEN** connected body A-B pushes connected body C-D
- **THEN** A-B 的 action unit 只提交 A-B 的 member changes
- **AND** C-D 的 movement 必须由独立 derived action unit 提交
- **AND** A-B waits and retries instead of committing C-D as its own side effect

### Requirement: Claim Arbitration Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for action-unit arbitration. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative move verification, and Unity EditMode tests for action unit intake, claim generation, handoff creation, parent retry, nested body handoff, single-unit planning, conflict, interrupt, and compatibility guards

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** player move, auto move, mechanism push, push handoff, port compatibility cases, body-to-body push, and WorldDelta observer sync remain consistent with server final state
- **AND** the manual report distinguishes OpenSpec validation, automated tests, server verification, and actual two-client runtime observation
