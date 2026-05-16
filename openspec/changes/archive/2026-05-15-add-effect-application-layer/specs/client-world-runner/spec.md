## ADDED Requirements
### Requirement: Client Mirrors Effect Results Only
Unity client SHALL consume server snapshot/delta data for effect-driven final Component/tag state and presentation metadata. Client code MUST NOT evaluate `EffectSpec`, `EffectApplication`, `RuntimeEffectStore`, stack policy, duration policy, or target hit rules to decide authoritative state.

#### Scenario: Server-applied effect updates client mirror
- **WHEN** the server applies a temporary pushable effect and sends a delta or snapshot
- **THEN** `ClientMapWorld` updates its mirror from the server final result
- **AND** client code does not locally decide whether the effect hit or whether pushability should be active

#### Scenario: Expiry is server authoritative
- **WHEN** a timed effect expires on the server
- **THEN** clients observe the final component/tag removal through server sync
- **AND** clients do not locally count down authoritative expiry to remove the result

### Requirement: Effect Presentation Metadata
Unity client SHALL be able to consume bounded effect presentation metadata from server deltas for visual feedback. Presentation metadata MAY include effect id, source entity, target entity/cell/body, cue id, start tick, expire tick, and result reason, but it MUST NOT be used by client code to change authoritative world state.

#### Scenario: Effect cue plays without changing rules
- **WHEN** a server delta contains an effect cue for temporary immobile
- **THEN** the client may play visual feedback for the target
- **AND** the cue does not modify the client authoritative mirror except through the accompanying final server state

#### Scenario: Missing cue does not break state sync
- **WHEN** an effect delta has no presentation cue id
- **THEN** the client still applies the final server snapshot/delta state
- **AND** only visual feedback is omitted
