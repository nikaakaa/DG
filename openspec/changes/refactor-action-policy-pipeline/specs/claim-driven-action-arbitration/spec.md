## MODIFIED Requirements

### Requirement: Claim-Driven Action Arbitration
系统 SHALL use action units resolved from `ActionSpec`, registered strategy modules, final component/tag facts, and runtime input to produce claim-driven arbitration results before planning or commit. `ActionClaim` MUST describe the current action unit's own finite claim over entities, cells, or resources. Ordinary action units MUST NOT claim an unbounded future chain, and central arbitration MUST NOT construct behavior-specific claim logic by action name.

#### Scenario: Move unit becomes claims
- **WHEN** a ready move-compatible action unit enters arbitration
- **THEN** the pipeline resolves its `ActionSpec`, source context, target, direction, current subject abstraction, final component/tag inputs, and registered strategy
- **AND** the strategy or claim collector produces claims for the current action unit before planner creates a plan
- **AND** the central arbitration stage only compares those claims

#### Scenario: Execution does not decide move policies
- **WHEN** the execution system processes ready action units
- **THEN** it delegates tag gate, target selection, subject selection, blocker checks, merge, interrupt, blocked policy, deferred output, and handoff decisions to explicit pipeline stages and registered policy modules
- **AND** it only orchestrates intake, arbitration result handling, planning, commit, deferred output enqueue, and result application

#### Scenario: Ordinary unit does not claim future chain movement
- **WHEN** a move action unit is blocked by a pushable target
- **THEN** the source unit records a finite blocked outcome or deferred output according to policy
- **AND** it does not claim all downstream bodies as one same-tick action-unit commit
- **AND** any continuing motion is represented as structured future ready action input

### Requirement: Strategy Registry Arbitration Boundary
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

### Requirement: Accepted Actions Feed Planning
系统 SHALL pass only accepted action units or accepted claims to planning. Planning MUST create plans from accepted arbitration output and MUST NOT rediscover pushable, bounce, or blocked policy outcomes. A source action that emits deferred output MUST NOT be treated as waiting on a removed pending chain before planning.

#### Scenario: Planner consumes accepted claims
- **WHEN** arbitration accepts a move action unit with body move claims
- **THEN** planner builds `MovePlan` members from accepted body move claims
- **AND** planner does not repeat tag gate, blocked branch matching, or ordinary behavior strategy selection

#### Scenario: Deferred output is future input
- **WHEN** a blocked action emits deferred output
- **THEN** execution enqueues structured future action input according to output policy
- **AND** the source action is not held in `PendingRuleStates` waiting for the downstream action result

#### Scenario: No old pending fallback
- **WHEN** a strategy or policy cannot express required waiting behavior
- **THEN** the implementation fails validation or requires a separate OpenSpec change
- **AND** it MUST NOT silently fall back to `PendingActionState`, `PendingActionUnit`, or removed parent retry semantics
