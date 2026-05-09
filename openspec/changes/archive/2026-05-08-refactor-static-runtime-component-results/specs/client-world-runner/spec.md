## ADDED Requirements
### Requirement: 客户端镜像最终 Component 结果
Unity 客户端 SHALL mirror server-authoritative final Component results from snapshot/delta. `ClientMapWorld` MUST NOT maintain a local runtime effect resolver, MUST NOT activate ability state locally, MUST NOT merge dynamic ports locally, and MUST NOT compute final Component results or movement permission from runtime effect data.

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
