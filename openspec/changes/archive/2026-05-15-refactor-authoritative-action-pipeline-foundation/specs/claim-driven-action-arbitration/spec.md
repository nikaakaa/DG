## MODIFIED Requirements

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
