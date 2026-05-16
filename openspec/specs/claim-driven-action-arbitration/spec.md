# claim-driven-action-arbitration Specification

## Purpose
TBD - created by archiving change refactor-action-claim-arbitration. Update Purpose after archive.
## Requirements
### Requirement: Claim-Driven Action Arbitration

系统 SHALL use action units resolved from `ActionContext`, `ActionSpec`, TargetData, ExecutionOutput, registered strategy modules, final component/tag facts, and runtime input to produce claim-driven arbitration results before planning or commit. `ActionClaim` MUST describe the current action unit's finite claim over entities, cells, or resources. Ordinary action units MUST NOT claim an unbounded future chain, and central arbitration MUST NOT construct behavior-specific claim logic by action name.

#### Scenario: Move unit becomes claims
- **WHEN** a ready move-compatible action unit enters arbitration
- **THEN** the pipeline resolves its `ActionContext`, `ActionSpec`, target data, execution output, current subject abstraction, final component/tag inputs, and registered strategy
- **AND** execution output produces claims for the current action unit before planner creates a plan
- **AND** the central arbitration stage only compares those claims

#### Scenario: Execution does not decide move policies
- **WHEN** the execution system processes ready action units
- **THEN** it delegates tag gate, target data query, subject selection, execution output generation, blocker checks, merge, interrupt, blocked policy, deferred output, and handoff decisions to explicit pipeline stages and registered policy modules
- **AND** it only orchestrates intake, arbitration result handling, planning, commit, deferred output enqueue, and result application

#### Scenario: Ordinary unit does not claim future chain movement
- **WHEN** a move action unit is blocked by a pushable target
- **THEN** the source unit records a finite blocked outcome or deferred output according to policy
- **AND** it does not claim all downstream bodies as one same-tick action-unit commit
- **AND** any continuing motion is represented as structured future ready action input

#### Scenario: Multiple target data produce multiple claims
- **WHEN** one execution output contains multiple target data results for the same action context
- **THEN** claim generation MAY create multiple finite claims for those targets
- **AND** claim arbitration resolves conflicts deterministically before planning

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

系统 SHALL pass only accepted action units, accepted claims, or accepted execution outputs to planning. Planning MUST create plans from accepted arbitration output and MUST NOT rediscover target selection, pushable, bounce, partial success, or blocked policy outcomes. A source action that emits deferred output MUST NOT be treated as waiting on a removed pending chain before planning.

#### Scenario: Planner consumes accepted claims
- **WHEN** arbitration accepts a move action unit with body move claims
- **THEN** planner builds `MovePlan` members from accepted body move claims
- **AND** planner does not repeat target data query, tag gate, blocked branch matching, ordinary behavior strategy selection, or success policy selection

#### Scenario: Deferred output is future input
- **WHEN** a blocked action emits deferred output
- **THEN** execution enqueues structured future action input according to output policy
- **AND** the source action is not held in `PendingRuleStates` waiting for the downstream action result

#### Scenario: No old pending fallback
- **WHEN** a strategy or policy cannot express required waiting behavior
- **THEN** the implementation fails validation or requires a separate OpenSpec change
- **AND** it MUST NOT silently fall back to `PendingActionState`, `PendingActionUnit`, or removed parent retry semantics

#### Scenario: Planning receives multi-target all-or-nothing intent
- **WHEN** an accepted execution output has required claims for multiple targets under all-or-nothing policy
- **THEN** planning and commit preserve the group result boundary
- **AND** they do not report action success when only a subset of required claims can commit

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
系统 SHALL select behavior construction through a strategy registry or equivalent typed module registry. The registry SHALL map typed primitive / strategy / policy keys to small behavior modules. Adding a new ordinary behavior that uses existing modules MUST NOT require editing the central arbitration class. Adding a new module MUST include tests that prove central claim arbitration remains generic. Strategy classes MAY declare registration metadata with attributes, but runtime arbitration MUST consume explicit generated registration code and injected registries, not reflection scanning.

#### Scenario: Existing strategy handles new behavior
- **WHEN** a new ordinary move-like action spec references an existing move strategy and existing policy data
- **THEN** the strategy creates equivalent action unit and claim structures for equivalent world state
- **AND** no central arbitration code is edited for that action id

#### Scenario: New strategy handles new primitive
- **WHEN** a new pull-line primitive cannot be expressed by existing move/spawn/remove strategies
- **THEN** a new registered strategy module MAY be added
- **AND** Luban action policy references the new strategy or primitive key
- **AND** central arbitration still only compares produced claims and priority

#### Scenario: Attribute generates explicit strategy registration
- **WHEN** a strategy class declares its key through an action-strategy attribute
- **THEN** the editor generator or Roslyn source generator emits explicit C# registration code for that strategy
- **AND** the emitted code constructs or resolves the strategy module and registers it into `ActionStrategyRegistry`
- **AND** the emitted code is the only source used by runtime assembly to discover that strategy

#### Scenario: Registered strategy reaches runtime execution
- **WHEN** a server-authoritative execution system is created with a strategy registry containing a default strategy set plus one new strategy module
- **THEN** a ready action whose `ActionSpec` resolves to that strategy is processed by the injected module during the real tick path
- **AND** `StateDrivenRuleExecutionSystem` does not construct a closed internal-only registry that prevents the new strategy from running
- **AND** `ActionArbiter` and execution orchestration do not add ordinary action-name branches for the new behavior

#### Scenario: Runtime does not scan attributes
- **WHEN** the server-authoritative tick path starts
- **THEN** it receives a prebuilt or generated strategy registry from composition code
- **AND** it does not scan loaded assemblies, inspect strategy attributes, or infer strategies from class names during tick execution
- **AND** missing or duplicate generated registrations fail during build, generation, or startup validation before gameplay ticks

#### Scenario: Registry is not a gameplay-name switch
- **WHEN** strategy registry resolves a module
- **THEN** it uses typed primitive / strategy keys from imported policy data
- **AND** it does not contain behavior-name-specific branches such as `wind_push`, `ice_slide`, or `trap_pull`

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

### Requirement: Targeting Precedes Execution And Claims
系统 SHALL run authoritative targeting before execution output and claim generation. Targeting MUST produce a read-only, deterministic `TargetData[]` candidate set from `ActionContext`, targeting selector, targeting policy, target filters, and `GameWorld`. Execution and claim generation MUST consume that candidate set rather than rediscovering target shape through action names or ad hoc spatial scans.

#### Scenario: move action consumes TargetData
- **WHEN** a ready move-compatible action enters arbitration
- **THEN** the targeting stage produces target data before move execution builds claims
- **AND** move execution consumes that target data to produce action claims
- **AND** planning and commit still validate accepted claims before writing world state

#### Scenario: Targeting 不裁决 blocked
- **WHEN** targeting returns a target cell or target entity that later proves occupied, blocked, reserved, or invalid for movement
- **THEN** blocked policy, arbitration, planning, or commit decides the result
- **AND** targeting does not derive push, bounce, reject, deferred output, or commit proposals

#### Scenario: selector 扩展不改仲裁职责
- **WHEN** a future `FrontLine`, `FrontEntities`, box, or circle selector class is added
- **THEN** it still returns candidate target data only
- **AND** arbitration, planning, and commit responsibilities remain unchanged

### Requirement: Deterministic Multi-Target Data
系统 SHALL make multi-target `TargetData[]` finite, bounded, and deterministically ordered. Multi-target output MUST include enough metadata for downstream fanout, diagnostics, and future effect binding without relying on dictionary order, Unity object order, client arrival order, or raw debug strings.

#### Scenario: selector stable ordering
- **WHEN** a selector returns several target entries for the same action
- **THEN** target data is ordered by explicit hit order and deterministic tie-breakers such as coordinate and entity id
- **AND** repeated runs over the same world state produce equivalent target data order

#### Scenario: multi-target fanout remains claim-driven
- **WHEN** a multi-target action produces several entity target data entries
- **THEN** execution may fan them out into multiple action claims
- **AND** each target claim still enters normal arbitration, planning, and commit
- **AND** target data alone does not move any entity

### Requirement: TargetData Metadata Boundary
`TargetData` SHALL describe candidate target facts such as target entity id, target coordinate, target body or subject key, hit cell, hit order, distance, direction, query id, and filter result. `TargetData` MUST NOT store pending lifecycle, action result ownership, commit state, or world mutation side effects.

#### Scenario: target body metadata supports later subject resolution
- **WHEN** targeting finds a target entity that belongs to a connected body
- **THEN** target data may include a body id or subject key for later layers
- **AND** subject resolution and connected body movement are still decided by subject policy, body capability resolution, claims, planning, and commit

#### Scenario: no world mutation
- **WHEN** targeting runs for any action
- **THEN** entity positions, components, tags, dirty state, runtime effects, and world delta buffers remain unchanged
- **AND** any world write must occur later through commit

### Requirement: Targeting Failure Is Structured
系统 SHALL return structured targeting failure when required target hints, direction, range, config, or filter inputs are invalid. Failure MUST carry stable error and reason data that action arbitration can map to action results without guessing from action names.

#### Scenario: missing direction
- **WHEN** an action requires direction from request but the context direction is `None`
- **THEN** targeting fails with stable invalid-direction data
- **AND** arbitration maps that failure to a rejected action result without executing claims

#### Scenario: target too far
- **WHEN** an action requires one-step target coordinate and the target is beyond one grid cell
- **THEN** targeting fails with stable too-far data
- **AND** no claim, plan, commit, or deferred output is created for that invalid target

