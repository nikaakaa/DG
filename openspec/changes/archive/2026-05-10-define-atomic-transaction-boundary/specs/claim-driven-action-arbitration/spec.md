## ADDED Requirements

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
