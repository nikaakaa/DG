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
系统 SHALL resolve blocked policies in the arbiter using `ActionSpec`, final component state, final tags, world state, and claims. Execution and planner MUST NOT hardcode whether a blocked move becomes push, bounce, or reject. When a blocked move derives another behavior, the arbiter SHALL use the resolved `ActionSpec` handoff policy to emit a derived action unit proposal and leave parent waiting / retry state to the pending action unit boundary.

#### Scenario: Start push if pushable
- **WHEN** an action with `StartPushIfPushable` hits an external blocker with final pushable component state
- **THEN** the arbiter derives a blocker push action unit proposal using the action's configured handoff policy
- **AND** the derived spec id and derived subject are resolved from policy data rather than hardcoded action names
- **AND** the original action unit enters waiting state instead of being replaced by the blocker action
- **AND** the original owner action does not report success until the parent unit retries and succeeds

#### Scenario: Bounce if bouncable
- **WHEN** an action with `BounceIfBouncable` hits a final bouncable blocker
- **THEN** the arbiter emits a bounce commit proposal or rejected result according to its `ActionSpec`
- **AND** execution and planner do not branch on an auto-move behavior name

### Requirement: Data-Driven Priority And Behavior Policy
系统 SHALL treat action priority, source, required tags, blocked tags, blocked policy, handoff policy, conflict policy, merge policy, interrupt policy, subject policy, plan rule, commit rule, and cost policy as data supplied by `ActionSpec` or the action spec registry. Core execution, planning, pending state, and commit orchestration MUST NOT hardcode ordinary behavior priorities or ordinary behavior policies.

#### Scenario: Priority comes from action spec
- **WHEN** a player move, auto move, mechanism push, configured wind push, or debug action is created
- **THEN** the request priority is resolved from its `ActionSpec` or explicit runtime override allowed by that spec
- **AND** the arbiter uses that priority data for merge and interrupt decisions

#### Scenario: Behavior policy comes from action spec
- **WHEN** a behavior changes from reject-on-block to start-push-if-pushable or bounce-if-bouncable
- **THEN** the change is expressed by updating behavior config or registry data
- **AND** `StateDrivenRules`, planner, pending state, and commit orchestration do not add behavior-name branches

#### Scenario: handoff spec comes from action spec
- **WHEN** an action derives a push handoff
- **THEN** the derived action's `ActionSpecId`, subject selection rule, and branch behavior come from the parent action's handoff policy
- **AND** pending state does not default an omitted spec id to `"player_push"`

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
系统 SHALL express pending push continuation as parent / derived action units in the same claim-driven arbiter. Pending push MUST NOT use execution-layer special branches for port front, chain expansion, final move acceptance, or same-tick whole-chain solving when those can be represented through reusable arbitration policies, action unit handoff, and push contact batches. A blocked action MAY create a push contact batch containing multiple derived child action units when its own move claims contact multiple distinct downstream pushable subjects in the same blocked step. Contacts MUST be resolved to handoff subjects before child unit creation, and multiple contacts resolving to the same downstream subject MUST create only one child unit. Those child units MUST remain independent action units with their own claims, arbitration, plans, and commits; execution MUST NOT combine parent and downstream bodies into one action-unit commit.

#### Scenario: Pending parent continues through claims
- **WHEN** a derived blocker action unit succeeds
- **THEN** the waiting parent action unit may continue through the existing pending action flow
- **AND** any later parent action unit enters the same claim-driven arbiter

#### Scenario: Pending completion waits for owner result
- **WHEN** a derived push action unit commits successfully
- **THEN** the owner action is not marked successful solely because the derived action succeeded
- **AND** the owner action result is reported only by the pending action boundary

#### Scenario: Pending push keeps chain safety
- **WHEN** an action unit derivation would loop, exceed pending timeout, or duplicate an active child subject outside an allowed same push contact batch
- **THEN** the pending action unit boundary fails the unit or batch with stable reason
- **AND** existing covered push outcomes remain compatible

#### Scenario: Body chain remains action-unit based
- **WHEN** connected body A is blocked by connected body B and B is blocked by connected body C
- **THEN** A, B, and C are represented as separate action units connected by handoff results or push contact batches
- **AND** execution does not create one combined A-B-C action-unit commit transaction

#### Scenario: Connected body front contacts create one push contact batch
- **WHEN** a connected body action unit moves in a direction and its body move claims target cells containing two distinct external pushable subjects
- **THEN** the blocked step creates one push contact batch owned by the parent action unit
- **AND** the batch contains one derived child action unit per distinct external pushable subject
- **AND** all child units share the parent owner action, parent unit id, direction, created tick, and batch id

#### Scenario: Multiple contacts resolving to one downstream subject create one child
- **WHEN** two body move claims contact two members of the same downstream connected body
- **THEN** the contact set may contain both contact facts
- **AND** handoff target resolution resolves both contacts to the same downstream subject key
- **AND** the blocked step creates one derived child action unit for that downstream subject
- **AND** the source body does not move as part of that handoff result

#### Scenario: Push contact batch does not merge downstream body commits
- **WHEN** a push contact batch contains child units for two downstream connected bodies
- **THEN** each downstream body produces and commits only through its own accepted child action unit
- **AND** the parent action unit does not include downstream body members in its own accepted claims or move plan

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

### Requirement: Handoff Policy Verification
系统 SHALL verify handoff behavior as explicit policy data. Tests MUST prove that changing handoff policy changes the derived action spec or subject selection without changing core arbiter, pending state, planner, or commit code.

#### Scenario: 两个 action 使用不同 handoff policy
- **WHEN** two move-like action specs hit the same pushable blocker but configure different handoff spec ids
- **THEN** arbitration creates derived action units with the configured spec ids
- **AND** no branch matches either action id string

#### Scenario: connected body handoff 不靠名字
- **WHEN** a pushable blocker belongs to a port connected body
- **THEN** derived subject selection follows the configured handoff subject policy
- **AND** it does not require a hardcoded `"connected_body_move"` spec id

### Requirement: Push Input Merge Boundary
系统 SHALL allow multiple same-tick push inputs to the same resolved subject to merge or deduplicate before planning and commit. Merge or deduplication MUST be based on the resolved subject key, not raw contact count, and MUST preserve deterministic conflict handling for different subjects.

#### Scenario: Same subject repeated input
- **WHEN** two contacts or action units in the same transaction resolve to the same downstream subject key
- **THEN** arbitration creates at most one consumable action unit or deferred output for that subject in the transaction
- **AND** the repeated input is not reported as `push chain cycle`
- **AND** raw contact count does not create duplicate child units

#### Scenario: Different subjects still remain separate
- **WHEN** two same-tick push inputs resolve to two distinct downstream subject keys
- **THEN** the system may create distinct action units or deferred outputs according to explicit policy
- **AND** those subjects are planned and committed independently

### Requirement: Unsafe Subject Overlap
系统 SHALL distinguish exact subject convergence from unsafe partial overlap. If a candidate subject has a different subject key but shares any entity with an already consumed or owned subject in the same transaction, the system MUST reject or fail the unsafe step with a stable safety reason.

#### Scenario: Exact subject convergence
- **WHEN** a candidate child subject has the same stable subject key as a subject already seen in the same pending state or transaction
- **THEN** the candidate is treated as convergence
- **AND** no duplicate child unit is created
- **AND** no chain-cycle failure is emitted for that exact duplicate

#### Scenario: Partial overlap remains unsafe
- **WHEN** a candidate child subject has a different stable subject key
- **AND** it shares at least one entity with a subject already consumed, owned, or added in the same transaction
- **THEN** the system rejects or fails that step with a stable unsafe-overlap or chain-cycle reason
- **AND** the shared entity is not committed by two action units

### Requirement: No Same-Transaction Feedback Amplification
系统 SHALL prevent closed-loop push feedback from being consumed repeatedly in the same transaction. Same-tick push strength or repeated push inputs MAY merge before the subject consumes, but feedback produced by that consumption MUST NOT recursively increase the same transaction's input.

#### Scenario: Multiple external inputs merge once
- **WHEN** several external sources push the same loop subject in one tick
- **THEN** their input may be merged for one subject consumption
- **AND** the loop subject consumes once for that tick

#### Scenario: Loop feedback does not amplify in-place
- **WHEN** a loop subject's consumed output routes back to the loop subject
- **THEN** the returned feedback is not consumed again in the same transaction
- **AND** it cannot create an unbounded same-tick push amplifier
- **AND** any continued effect is delayed to a later tick through a structured deferred output

### Requirement: Policy-Driven Deferred Output
系统 SHALL represent continued push output with explicit deferred output policy data. Runtime arbitration MUST NOT infer deferred output behavior from action names, entity names, tag combinations, or hard-coded mechanism identifiers.

#### Scenario: Deferred output fields drive behavior
- **WHEN** a blocked action produces continued downstream output
- **THEN** the output contains source subject, target subject, intent, direction or vector, ready tick, cost, dedupe key, and causality id
- **AND** the next ready action is created from those fields
- **AND** the rules layer does not branch on a special action name or mechanism name to choose this behavior

#### Scenario: Equivalent policies behave equivalently
- **WHEN** two action specs have different ids but the same deferred output policy fields
- **THEN** they produce equivalent deferred output behavior for the same world state
- **AND** differences in behavior require explicit policy data differences

### Requirement: Move Claim Contact Set
系统 SHALL discover external blockers for a move action unit by evaluating the action unit's produced body move claims. The contact set MUST include contact facts for blocking entities occupying claimed target coordinates, excluding entities already inside the current action subject/body. The contact set MUST NOT decide the downstream handoff subject or the number of child units. The blocked policy layer MUST consume this contact set instead of only the first blocker when deriving push handoff units.

#### Scenario: 多 member 目标格收集多个 external contacts
- **WHEN** a connected body action unit has two body move claims whose target coordinates are occupied by two distinct external entities
- **THEN** the arbiter discovers both external entities in the contact set
- **AND** contacts already belonging to the moving body are excluded
- **AND** handoff child unit creation is decided later by resolved downstream subject

#### Scenario: 单 blocker 兼容
- **WHEN** a single-entity move action has one target coordinate occupied by one external pushable entity
- **THEN** the contact set contains one contact
- **AND** existing single-blocker push behavior remains representable

#### Scenario: 同一 downstream subject 保留 contact 事实
- **WHEN** two body move claims target two members of the same external connected body
- **THEN** the contact set may preserve both contact facts
- **AND** contact collection does not collapse them by connected body
- **AND** later handoff target resolution deduplicates child units by the resolved subject key

### Requirement: Push Contact Batch Completion
系统 SHALL define how one blocked step with multiple push contacts reports success or failure independently from individual action unit claim arbitration. The first implementation MUST support fixed all-success semantics: all child units in the batch must succeed before the parent may continue as unblocked; any child unit failure fails the batch and prevents parent success for that blocked step.

#### Scenario: batch 全部 child 成功
- **WHEN** all child units in a push contact batch are accepted and committed
- **THEN** the batch is marked succeeded
- **AND** the parent unit may continue through the normal pending action flow

#### Scenario: batch 任一 child 失败
- **WHEN** any child unit in a push contact batch is rejected, interrupted, times out, or fails chain safety
- **THEN** the batch is marked failed
- **AND** the parent unit does not continue as if the blocked path was cleared
- **AND** no successful child commit is reported as the owner action's final success by itself

### Requirement: Pending Push Subject Safety
系统 SHALL keep pending push safety focused on resolved action-unit subjects rather than raw contact count. Pending state MUST reject true cycles, duplicate active downstream subjects, and exceeded timeout. Pending state MUST NOT reject push propagation merely because it has derived many action units, and it MUST NOT treat multiple contacts or multiple members inside one resolved connected body subject as propagation depth.

#### Scenario: sibling branches converge on one ready downstream subject
- **WHEN** two sibling child requests inside one pending push state resolve to the same downstream subject
- **THEN** pending state does not create a duplicate ready child unit for that subject
- **AND** the duplicate subject is not reported as a push chain cycle

#### Scenario: true ancestor subject revisit fails
- **WHEN** a derived push would create a child whose resolved subject includes an ancestor subject already in the chain
- **THEN** pending state rejects that child creation with a stable chain cycle reason
- **AND** no extra child unit is created for the unsafe step

#### Scenario: propagation length is not a failure condition
- **WHEN** push propagation derives several action units without revisiting an ancestor subject
- **THEN** pending state does not fail solely because of propagation length
- **AND** each derived action unit remains atomic and continues through normal arbitration and commit

### Requirement: Collision-Safe Composed Push Movement
系统 SHALL resolve composed push movement through discrete grid movement checks. A composed push vector MUST NOT teleport, fly over, or otherwise bypass intermediate collision cells.

#### Scenario: 单轴合成仍走 claim 和 commit
- **WHEN** push vector composition produces a non-zero single-axis intent
- **THEN** the resulting movement attempt enters the existing body claim, target claim, and commit validation pipeline
- **AND** blockers, reserved targets, source changes, and occupied cells can still reject the movement

#### Scenario: 向量终点不能跳过碰撞
- **WHEN** a composed vector has magnitude greater than one or contains multiple axis components
- **THEN** the system MUST NOT directly move the subject to the vector endpoint
- **AND** every occupied grid cell that would be crossed by an enabled path policy must be checked as a discrete movement step

#### Scenario: 双轴向量确定性拆分
- **WHEN** push vector composition produces a non-zero two-axis vector
- **THEN** the system decomposes it into deterministic single-cell steps using the configured path order
- **AND** the first implementation uses X then Y as the default path order
- **AND** each step is checked through discrete movement validation
- **AND** future eight-direction movement still requires explicit collision or corner-crossing rules before diagonal shortcuts are allowed

### Requirement: Push Composition Does Not Replace Existing Arbitration
系统 SHALL keep existing body intent arbitration, target claim arbitration, and commit validation as the final movement safety layers after push vector composition.

#### Scenario: body intent 仍然裁决
- **WHEN** a composed push intent targets a connected body
- **THEN** the body still projects its member claims through the existing claim pipeline
- **AND** all member movement remains all-or-nothing according to the existing body commit boundary

#### Scenario: target claim 仍然裁决
- **WHEN** a composed push intent and another accepted move attempt target the same cell
- **THEN** target claim arbitration still chooses or rejects candidates according to existing priority and claim rules
- **AND** push vector composition does not reserve target cells by itself

