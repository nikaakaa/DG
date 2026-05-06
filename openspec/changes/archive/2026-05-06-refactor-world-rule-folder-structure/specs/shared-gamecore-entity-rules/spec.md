## ADDED Requirements
### Requirement: Shared Rules Source Layout
Shared GameCore rule execution code SHALL live under a directory that represents world rules instead of a single movement mechanic.

#### Scenario: Rules pipeline has domain-level folder
- **WHEN** a developer looks for action, intent, planning, arbitration, pending state, or commit code
- **THEN** those files are discoverable under `Shared/DG.GameCore/Rules`
- **AND** `Shared/DG.GameCore/Movement` is not the primary entry point for the rule pipeline

#### Scenario: Legacy movement folder is not a rule entry point
- **WHEN** the legacy movement resolver has no production references
- **THEN** empty or obsolete `Movement/Legacy` folders do not remain as an apparent extension point

### Requirement: Rule Pipeline File Boundaries
The rule pipeline SHALL keep intent definition, arbitration, planning, conflict resolution, pending state, and rule execution in separate source file boundaries while preserving the same runtime behavior.

#### Scenario: Intent definitions are separate from arbitration execution
- **WHEN** a new `BehaviorIntentKind` default policy is reviewed
- **THEN** its source tag, ability tag, required tags, blocked tags, and cancel policy are found in the intent definition boundary
- **AND** the arbiter does not contain per-kind switch inference logic

#### Scenario: Planning and commit remain distinct
- **WHEN** a move intent is accepted by arbitration
- **THEN** planning produces a move plan before commit
- **AND** conflict resolution remains responsible for same-tick atomic commit decisions
