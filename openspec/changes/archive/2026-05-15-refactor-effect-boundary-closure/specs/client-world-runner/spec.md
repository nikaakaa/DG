## MODIFIED Requirements
### Requirement: 客户端镜像最终 Component 结果
Unity 客户端 SHALL mirror server-authoritative final Component results from snapshot/delta. `ClientMapWorld` MUST NOT maintain a local runtime effect resolver, MUST NOT activate ability state locally, MUST NOT merge dynamic ports locally, and MUST NOT compute final Component results or movement permission from runtime effect data. Server-authoritative client submitters and debug tools MUST NOT apply or remove runtime effects locally as an authority fallback.

#### Scenario: Snapshot applies final result
- **WHEN** the client receives a server snapshot or delta that contains final `BlockingComponent`, `AutoMoveComponent`, `PushableComponent`, `PortConnectorComponent`, or movement permission state
- **THEN** `ClientMapWorld` applies that final state into its Shared `GameWorld` mirror
- **AND** Unity view code reads the mirrored final state for display

#### Scenario: Client does not resolve effect
- **WHEN** a server-side runtime effect creates or removes temporary component results
- **THEN** the client waits for server snapshot/delta carrying the final result
- **AND** the client does not evaluate `RuntimeEffectSpec`, `RuntimeEffectInstance`, `AbilityKind`, or `EffectKind` locally to decide authority state

#### Scenario: Client does not merge port or movement permission
- **WHEN** a server-side runtime effect changes port connectivity or movement permission
- **THEN** the client applies only the server-synchronized final result
- **AND** the client does not locally merge port sources
- **AND** the client does not locally decide whether movement should be accepted or rejected

#### Scenario: Manual observer sees final result
- **WHEN** client A applies and removes a temporary runtime effect through an approved debug or test path
- **AND** client B is observing the same server world
- **THEN** client B sees only the server-synchronized final Component result changes
- **AND** client B does not need the effect source data to display the final state

#### Scenario: Server authoritative submitter does not fallback locally
- **WHEN** a runtime debug effect RPC cannot be sent because session or networking is unavailable
- **THEN** the server-authoritative submitter reports failure or unavailable state
- **AND** it does not call local runtime effect add/remove APIs to mutate the authoritative mirror

#### Scenario: Offline effect tooling is explicitly separated
- **WHEN** offline sandbox or EditMode tooling needs local runtime effect simulation
- **THEN** it is wired through a clearly named local-only path
- **AND** tests prove that server-authoritative Play Mode wiring does not use that local-only path

## ADDED Requirements
### Requirement: Client Effect Debug Intent Boundary
Unity debug UI SHALL treat runtime effect controls as server intent submitters in server-authoritative mode. The UI MAY display requested effect kind, effect id, cue, or callback reason, but authoritative mirror changes MUST come from server response, snapshot, or WorldDelta.

#### Scenario: Debug apply sends intent only
- **WHEN** the developer clicks a debug apply runtime effect button in server-authoritative Play Mode
- **THEN** the client sends the configured debug RPC or request
- **AND** it does not add a local runtime effect to the Shared `GameWorld` mirror before server sync

#### Scenario: Debug remove sends intent only
- **WHEN** the developer clicks a debug remove runtime effect button in server-authoritative Play Mode
- **THEN** the client sends the configured debug remove RPC or request
- **AND** it does not remove local final Component/tag state unless a server delta or snapshot applies the final result
