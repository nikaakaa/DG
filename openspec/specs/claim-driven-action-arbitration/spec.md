# claim-driven-action-arbitration Specification

## Purpose
TBD - created by archiving change refactor-action-claim-arbitration. Update Purpose after archive.
## Requirements
### Requirement: Claim-Driven Action Arbitration
系统 SHALL use `BehaviorActionUnit + ActionSpec` to produce claim-driven arbitration results before planning or commit. `ActionClaim` MUST describe the current action unit's own claim over its source entity or current entity abstraction; ordinary action units MUST NOT claim a chain of unrelated body members as one default atomic commit.

#### Scenario: Move unit becomes claims
- **WHEN** a move behavior action unit enters arbitration
- **THEN** the arbiter resolves its `ActionSpec`, source context, target, direction, current unit entity abstraction, and final component/tag inputs
- **AND** the arbiter produces claims for the current action unit before planner creates a plan

#### Scenario: Execution does not decide move policies
- **WHEN** `StateDrivenRules` receives behavior action units for a tick
- **THEN** it delegates target resolution, blocker checks, merge, interrupt, blocked policy, and handoff decisions to the action arbiter or configured policy layer
- **AND** it only orchestrates intake, arbitration, result branch handling, planning, commit, pending/handoff state update, and result application

#### Scenario: Ordinary unit does not claim pushed target movement
- **WHEN** a source unit hits a pushable target
- **THEN** the source unit records a handoff result instead of claiming the target entity's movement in the same unit
- **AND** the target entity's movement requires its own derived behavior action unit

### Requirement: Port-Connected Body Claim Projection
系统 SHALL keep port-connected body projection isolated as compatibility semantics or a distinct entity abstraction. The arbiter MAY evaluate every member target cell for explicitly marked port-connected scenarios, but ordinary push and movement action unit atomicity MUST NOT depend on port-connected multi-member body projection.

#### Scenario: Linked port member hits pushable
- **WHEN** a player is port-connected to another blocking member
- **AND** the change explicitly treats that connected body as a compatibility entity abstraction
- **THEN** the arbiter may treat the connected body as the action unit's current abstraction
- **AND** this does not define the ordinary push handoff model for non-port actions

#### Scenario: Same body internal cells are not external blockers
- **WHEN** a connected body moves into cells previously occupied by members of the same body under compatibility coverage
- **THEN** those cells do not create external blocker claims
- **AND** the accepted action can remain a port-specific abstraction until a follow-up change defines the final port model

#### Scenario: Port model is not completed by this change
- **WHEN** this change is reviewed or validated
- **THEN** tests and docs must state that final port-connected body modeling is out of scope
- **AND** port compatibility must not be used to justify ordinary multi-entity action unit commits

### Requirement: Data-Driven Blocked Policy Resolution
系统 SHALL resolve blocked policies in the arbiter using `ActionSpec`, final component state, final tags, world state, and claims. Execution and planner MUST NOT hardcode whether a blocked move becomes push, bounce, or reject. When a blocked move derives another behavior, the arbiter SHALL emit a derived action unit proposal and leave parent waiting / retry state to the pending action unit boundary.

#### Scenario: Start push if pushable
- **WHEN** an action with `StartPushIfPushable` hits an external blocker with final pushable component state
- **THEN** the arbiter derives a blocker push action unit proposal
- **AND** the original action unit enters waiting state instead of being replaced by the blocker action
- **AND** the original owner action does not report success until the parent unit retries and succeeds

#### Scenario: Bounce if bouncable
- **WHEN** an action with `BounceIfBouncable` hits a final bouncable blocker
- **THEN** the arbiter emits a bounce commit proposal or rejected result according to its `ActionSpec`
- **AND** execution and planner do not branch on an auto-move behavior name

### Requirement: Data-Driven Priority And Behavior Policy
系统 SHALL treat action priority, source, required tags, blocked tags, blocked policy, conflict policy, merge policy, interrupt policy, plan rule, and commit rule as data supplied by `ActionSpec` or the action spec registry. Core execution, planning, and commit orchestration MUST NOT hardcode ordinary behavior priorities or ordinary behavior policies.

#### Scenario: Priority comes from action spec
- **WHEN** a player move, auto move, mechanism push, configured wind push, or debug action is created
- **THEN** the request priority is resolved from its `ActionSpec` or explicit runtime override allowed by that spec
- **AND** the arbiter uses that priority data for merge and interrupt decisions

#### Scenario: Behavior policy comes from action spec
- **WHEN** a behavior changes from reject-on-block to start-push-if-pushable or bounce-if-bouncable
- **THEN** the change is expressed by updating behavior config or registry data
- **AND** `StateDrivenRules`, planner, and commit orchestration do not add behavior-name branches

### Requirement: Accepted Actions Feed Planning
系统 SHALL pass only accepted action units or accepted claims to planning. Planning MUST create plans from accepted arbitration output and MUST NOT rediscover pushable, bounce, or blocked policy outcomes. A parent action unit that is waiting for a derived action unit MUST NOT be planned until it retries and is accepted on its own ready tick.

#### Scenario: Planner consumes accepted claims
- **WHEN** arbitration accepts a move action unit with body move claims
- **THEN** planner builds `MovePlan` members from accepted body move claims
- **AND** planner does not re-check whether the target blocker should be pushed or bounced

#### Scenario: Waiting parent action does not plan
- **WHEN** the arbiter derives a blocker action unit from a blocked parent action unit
- **THEN** planner does not build a move plan for the waiting parent action unit
- **AND** action result and pending action unit state reflect that the parent is waiting

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

### Requirement: Ordinary Behavior Extension Does Not Modify Core Orchestration
系统 SHALL allow new ordinary move-like behavior to reuse existing primitives, claim kinds, conflict policies, merge policies, interrupt policies, blocked policies, and commit policies without editing `StateDrivenRules`, `ActionArbiter`, `RulePlanner`, or commit orchestration.

#### Scenario: Add configured wind push
- **WHEN** a configured `wind_push` behavior uses existing move primitive and claim policies
- **THEN** adding or changing the behavior requires only data/registry/test updates
- **AND** no new execution or arbitration orchestration branch is required

#### Scenario: New reusable policy is explicit core extension
- **WHEN** a behavior needs a policy not representable by existing policy types
- **THEN** adding that policy is treated as a core extension
- **AND** the extension includes focused tests proving existing policies remain unchanged

### Requirement: Claim Arbitration Verification
系统 SHALL include automated Unity TestFramework EditMode coverage and Shared/server validation for action-unit arbitration. Unity Player build MUST NOT be required.

#### Scenario: Automated verification
- **WHEN** automated validation runs
- **THEN** it includes OpenSpec strict validation, Shared GameCore build, server authoritative move verification, and Unity EditMode tests for action unit intake, claim generation, handoff creation, parent retry, nested body handoff, single-unit planning, conflict, interrupt, and compatibility guards

#### Scenario: Manual end-to-end verification
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** player move, auto move, mechanism push, push handoff, port compatibility cases, body-to-body push, and WorldDelta observer sync remain consistent with server final state
- **AND** the manual report distinguishes OpenSpec validation, automated tests, server verification, and actual two-client runtime observation

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

