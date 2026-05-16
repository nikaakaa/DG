## MODIFIED Requirements

### Requirement: Shared Rules Source Layout
Shared GameCore behavior execution code SHALL live under source folders that represent the final Shared runtime architecture instead of a generic `Rules` entry point or a single movement mechanic. The authoritative behavior pipeline SHALL use `Shared/DG.GameCore/ActionRuntime` as the primary source entry for action intake, lifecycle, gating, subject selection, targeting, strategy execution, claim arbitration, blocking, planning, commit, and generated action-runtime registration. Shared domain data, world storage, spatial indexes, snapshot/delta projection, Luban configuration import, Luban generated data, runtime effects, and tests SHALL live in their own sibling folders rather than under action runtime orchestration.

#### Scenario: Action runtime replaces generic Rules entry
- **WHEN** a developer looks for action, intent, lifecycle, targeting, blocking, arbitration, planning, or commit code
- **THEN** those files are discoverable under `Shared/DG.GameCore/ActionRuntime`
- **AND** `Shared/DG.GameCore/Rules` is not the long-term extension entry point
- **AND** `Shared/DG.GameCore/Movement` is not the primary entry point for the behavior pipeline

#### Scenario: Shared folders express runtime boundaries
- **WHEN** a developer looks for component models, entity ids, world storage, spatial indexes, snapshot/delta data, Luban import, Luban generated tables, runtime effects, or tests
- **THEN** those files are discoverable under `Domain`, `World`, `Configuration`, `RuntimeEffects`, or `Testing`
- **AND** those folders do not depend on ordinary action names to decide behavior
- **AND** Shared GameCore remains pure C# without UnityEngine, Fantasy runtime, or protocol generated type dependencies

#### Scenario: Generated code is isolated
- **WHEN** action-runtime generated registration or Luban generated table code is reviewed
- **THEN** the files live under a `Generated` folder for their owning area
- **AND** generated files do not contain hand-written ordinary behavior policy, world mutation logic, targeting decisions, or blocked outcome business branches
- **AND** hand-written runtime code consumes generated registration through explicit registry/provider boundaries

#### Scenario: Business outcomes are not hidden in low-level executor files
- **WHEN** blocked behavior such as derive, bounce, reject, or noop is reviewed
- **THEN** the business outcome implementation is found under `ActionRuntime/Blocking/Outcomes` or an equivalent focused policy module folder
- **AND** the central blocked outcome executor only dispatches resolved policy outcomes
- **AND** new blocked business behavior does not require adding ordinary action-name branches to a central executor

#### Scenario: Legacy movement folder is not a rule entry point
- **WHEN** the legacy movement resolver has no production references
- **THEN** empty or obsolete `Movement/Legacy` folders do not remain as an apparent extension point

### Requirement: Rule Pipeline File Boundaries
The action runtime pipeline SHALL keep action definitions, policy data models, request models, tag/component gates, subject selection, target selection, strategy registry, strategy modules, claim arbitration, blocking contact collection, blocked policy matching, blocked outcome modules, planning, conflict resolution, deferred output enqueue, commit proposal models, commit handler registry, commit handlers, and generated registration in separate source file boundaries while preserving the same runtime behavior. No single central class SHALL own all ordinary behavior construction, blocked outcome execution, claim arbitration, and commit mutation.

#### Scenario: Action definitions are separate from execution
- **WHEN** a new `ActionSpec` default policy is reviewed
- **THEN** its source, required tags, blocked tags, cost, strategy, blocked result policy, merge policy, interrupt policy, plan rule, and commit rule are found in the action definition or policy boundary
- **AND** central arbitration execution is not the source of those policy values

#### Scenario: Strategy modules are separate from central arbitration
- **WHEN** a behavior needs a new reusable low-level capability
- **THEN** the code adds or updates a strategy module and registry entry
- **AND** the registry entry is produced from strategy metadata into explicit generated C# registration code
- **AND** central arbitration continues to compare produced action units and claims

#### Scenario: Generated registration is outside rule decisions
- **WHEN** generated strategy registration code is reviewed
- **THEN** it only maps typed strategy keys to strategy module construction or registration
- **AND** it does not contain ordinary action id branches, world-state decisions, tag-condition evaluation, planning, or commit logic
- **AND** rule systems consume the completed registry through dependency injection

#### Scenario: Blocking contacts, policies, and outcomes are separate
- **WHEN** a move-like action is blocked by an occupied target cell
- **THEN** contact collection produces blocker facts
- **AND** blocked policy matching chooses the configured outcome
- **AND** the chosen outcome module performs derive, bounce, reject, noop, or future configured behavior
- **AND** the central action arbiter does not encode those ordinary blocked business branches directly

#### Scenario: Planning and commit remain distinct
- **WHEN** accepted action claims are available
- **THEN** planning turns them into move/spawn/remove/component/effect proposals
- **AND** commit applies proposals atomically through commit handlers
- **AND** conflict resolution remains responsible for same-tick atomic commit decisions

#### Scenario: Deferred output is separate from commit
- **WHEN** an action emits deferred output
- **THEN** deferred output enqueue is represented as structured future action input
- **AND** commit does not own push propagation lifecycle
- **AND** removed pending retry code is not used as a fallback
