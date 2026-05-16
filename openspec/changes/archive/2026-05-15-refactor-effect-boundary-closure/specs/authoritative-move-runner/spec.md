## ADDED Requirements
### Requirement: 服务端权威 Runtime Effect 调试执行
服务端 SHALL execute debug runtime effect apply/remove through the same commit and effect spec boundary as ordinary effect application. Debug effect requests MUST resolve to an `EffectSpecId` and an `EffectSpec` from Luban-backed config or an explicit debug effect registry before producing `EffectApplication`. Debug execution MUST NOT construct ad hoc authoritative `EffectSpec` objects from `RuntimeEffectKind` as the main path.

#### Scenario: Debug apply resolves effect spec
- **WHEN** a client sends a debug apply runtime effect request using the legacy effect kind input
- **THEN** the server maps that input to an explicit effect spec id
- **AND** loads the effect definition from provider or debug registry
- **AND** creates an `EffectApplication` from that definition
- **AND** submits `AddRuntimeEffect` through commit

#### Scenario: Debug remove uses runtime source identity
- **WHEN** a client sends a debug remove runtime effect request with runtime effect id
- **THEN** the server submits `RemoveRuntimeEffect` through commit
- **AND** only the matching runtime effect source is removed
- **AND** final Component/tag state is recomputed by resolver

#### Scenario: Debug failure does not mutate world
- **WHEN** effect kind cannot resolve to an effect spec id, effect spec is missing, entity is missing, or debug editing is disabled
- **THEN** the server responds with failure reason
- **AND** no runtime effect source, final Component, final tag, or WorldDelta is produced

#### Scenario: Debug apply broadcasts final result
- **WHEN** debug apply runtime effect succeeds
- **THEN** the authoritative GameWorld changes only through commit and resolver
- **AND** observers receive the same final Component/tag result through WorldDelta

### Requirement: Runtime Effect Debug Verification
系统 SHALL verify server debug runtime effect behavior through server authoritative verification and manual two-client validation.

#### Scenario: Server verification covers debug effect
- **WHEN** server authoritative verification runs
- **THEN** it covers debug apply effect, debug remove effect, missing effect spec failure, and static source preservation
- **AND** it proves debug effect execution does not bypass commit

#### Scenario: Manual debug effect sync
- **WHEN** client A applies and removes runtime effects through debug UI
- **AND** client B observes the same server world
- **THEN** both clients converge to server final Component/tag state
- **AND** client B does not need runtime effect source data to display the result
