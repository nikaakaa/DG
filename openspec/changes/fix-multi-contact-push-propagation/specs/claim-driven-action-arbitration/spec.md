## MODIFIED Requirements
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

### Requirement: Connected Body Member Commit Boundary
系统 SHALL limit multi-member movement commits to an accepted connected body subject. Multi-member commit MUST NOT be used to model ordinary push chains, parent continuation, downstream blocker movement, or unrelated entity side effects. A push contact batch MAY relate multiple action units for multi-contact push propagation and result aggregation, but it MUST NOT turn those units into one combined member commit.

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

#### Scenario: body-to-body push chain 不扩展 member commit
- **WHEN** connected body A-B pushes connected body C-D
- **THEN** A-B 的 action unit 只提交 A-B 的 member changes
- **AND** C-D 的 movement 必须由独立 derived action unit 提交
- **AND** A-B does not commit C-D as its own side effect

#### Scenario: push contact batch 聚合结果不聚合 member
- **WHEN** one parent action unit derives two child action units in the same push contact batch
- **THEN** batch result aggregation MAY decide parent continuation, batch failure, or owner result timing
- **AND** batch aggregation MUST NOT add any child subject member to the parent unit's commit member set

## ADDED Requirements
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
