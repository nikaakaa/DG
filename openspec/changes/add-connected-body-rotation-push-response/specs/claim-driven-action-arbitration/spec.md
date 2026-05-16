## ADDED Requirements
### Requirement: Torque Arbitration Uses Push Contributions
The action arbitration pipeline SHALL preserve same-ready-tick push contribution data for rotate-pivot response. Torque arbitration SHALL be a reusable module or equivalent explicit policy that consumes contribution direction, touched member, connected body subject, contribution count, causality samples, and push origin contexts. It MUST NOT replace ordinary push vector composition for non-rotating subjects. Rotate-pivot impact output SHALL enter the same push contribution model as ordinary push output, with rotate impact origin context preserved as contribution data rather than presentation-only metadata.

#### Scenario: rotate subject uses torque arbitration
- **WHEN** push contributions target the same rotate-pivot connected body subject in the same ready tick
- **THEN** torque arbitration evaluates those contributions before ordinary translation planning for that subject
- **AND** the subject attempts rotate response when torque arbitration leaves a non-zero direction

#### Scenario: ordinary push vector behavior remains separate
- **WHEN** push contributions target a subject without exactly one rotate pivot
- **THEN** existing push vector composition and ordinary connected-body push planning remain responsible for that subject
- **AND** torque arbitration does not change the ordinary push vector result

#### Scenario: merged contribution metadata survives rotate response
- **WHEN** multiple push requests merge into one rotate response representative
- **THEN** the result keeps contribution count, per-direction or torque summary, causality samples, push origin contexts, and merged request references available for diagnostics and output metadata
- **AND** the central arbiter does not infer rotate behavior from action names

#### Scenario: rotate impact push is ordinary push contribution
- **WHEN** a blocked rotate-pivot response emits downstream push output
- **THEN** the downstream action request contains push contribution data equivalent to ordinary deferred push
- **AND** its origin context identifies the rotate-pivot impact member, impact from/to, blocker, pivot, rotate direction, and source causality
- **AND** later push vector or torque arbitration can consume that request without a rotate-specific action-name branch

### Requirement: Rotate Response Enters Finite Action Output
The action pipeline SHALL represent rotate-pivot response as finite current-tick planning or deferred output. A rotate response that emits downstream push output MUST cancel its own movement and create structured deferred actions. It MUST NOT wait for downstream push success, solve downstream bodies in the same action-unit commit, or recreate removed push pending chain behavior.

#### Scenario: successful rotate commits only rotating body
- **WHEN** torque arbitration accepts one rotate-pivot connected body and the rotate target plan has no external blockers
- **THEN** the current action unit may commit only the rotating body's member movement
- **AND** it does not also commit ordinary translation for that body

#### Scenario: blocked rotate emits deferred actions
- **WHEN** torque arbitration accepts one rotate-pivot connected body and target planning finds external pushable blockers
- **THEN** the current action unit emits structured deferred push output for every distinct downstream subject
- **AND** the current action unit does not wait for those downstream actions to succeed
- **AND** the current action unit does not include downstream body members in its own move plan
- **AND** each deferred action carries the precise rotate impact context needed to reproduce the push contribution

#### Scenario: rotate-pivot subject has one result per tick
- **WHEN** a rotate-pivot connected body consumes same-ready-tick push contributions
- **THEN** arbitration resolves one final behavior result for that subject in that tick
- **AND** it does not schedule both ordinary translation and rotate response for that same resolved subject

#### Scenario: zero torque consumed input is invalid
- **WHEN** a rotate-pivot connected body consumes push contributions whose torque result is zero
- **THEN** arbitration marks the consumed input invalid or no-op for that subject
- **AND** it does not pass the same input into ordinary translation planning
