## ADDED Requirements

### Requirement: Atomic Transaction Consumption Boundary
系统 SHALL treat each server tick's ready action processing as a finite atomic transaction boundary. Within one transaction, a resolved action subject MUST consume at most one merged push input, and any downstream push output produced by that consumption MUST NOT be consumed again by the same subject inside the same transaction.

#### Scenario: Same subject consumes once per tick
- **WHEN** two ready action units in the same server tick both resolve push input into the same subject
- **THEN** the subject consumes at most one merged input for that tick
- **AND** the system does not create two independent movement commits for the same subject
- **AND** no entity in that subject is committed by two action units in the same transaction

#### Scenario: Feedback waits for future tick
- **WHEN** a consumed push output would route back to a subject already consumed in the current transaction
- **THEN** the system does not consume that feedback again in the current transaction
- **AND** any continuing output is represented as a structured deferred output no earlier than `tick + cost`
- **AND** the current transaction remains finite

### Requirement: Emergent Motion Across Atomic Transactions
系统 SHALL model continuous or infinite-appearing motion as repeated finite transactions across ticks. A closed-loop device MUST NOT require a single action or pending state to recursively solve the complete loop before committing a result.

#### Scenario: Loop produces periodic output
- **WHEN** an external push activates a closed-loop device
- **AND** the loop still has a valid output path after one finite transaction
- **THEN** the current transaction commits only its finite result
- **AND** the next output is evaluated in a later tick according to the configured cost
- **AND** repeated outputs emerge from repeated tick processing rather than one unbounded action chain

#### Scenario: Loop without output does not hang
- **WHEN** a closed-loop device routes push back into itself without any external output
- **THEN** the transaction does not recursively expand forever
- **AND** the loop subject is not repeatedly consumed in the same transaction
- **AND** the transaction completes with a bounded no-output result instead of remaining active indefinitely

#### Scenario: Surging push device emits periodic output
- **WHEN** an external push activates a closed-loop device with a valid external output port
- **AND** each push has cost `a`
- **THEN** the first transaction consumes the loop subject once
- **AND** feedback to the loop subject is not consumed again in the same transaction
- **AND** the system records one deferred output with `ready tick = current tick + a`
- **AND** repeated output cadence is produced by later ready outputs, not by same-transaction recursion

#### Scenario: No first-phase strength amplification
- **WHEN** a closed-loop device feeds back into itself
- **AND** no explicit strength policy is configured
- **THEN** repeated outputs do not increase push strength across ticks
- **AND** the device is treated as a periodic output device rather than a push amplifier

### Requirement: Atomic Transaction Verification
系统 SHALL include automated validation for single-consumption subject behavior, feedback deferral, and closed-loop finite transaction boundaries. Unity Player build MUST NOT be required.

#### Scenario: Automated validation
- **WHEN** automated validation runs
- **THEN** it includes Unity TestFramework EditMode coverage for same-subject repeated push input, closed-loop feedback truncation, and no duplicate same-tick subject commits
- **AND** it includes Shared GameCore build and server authoritative verification

#### Scenario: Manual validation
- **WHEN** the user manually runs server-authoritative Play Mode with a closed-loop push device
- **THEN** the device does not freeze one action in an unbounded pending chain
- **AND** any continuing output is observed as later tick results
- **AND** two clients observe the same server WorldDelta sequence
