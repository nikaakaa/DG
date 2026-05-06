## ADDED Requirements
### Requirement: Unity ClientWorld Source Layout
Unity client world runtime code SHALL live under a folder that represents the client world module instead of a generic map module.

#### Scenario: Client world files are discoverable
- **WHEN** a developer looks for client world bootstrap, runtime tick, networking, view, debug, input, interaction, or spatial adapter code
- **THEN** those files are discoverable under `Client/DG_Client/Assets/Scripts/ClientWorld`

#### Scenario: Map naming does not hide world mirror responsibility
- **WHEN** a developer reviews the client runtime directory
- **THEN** the folder naming makes it clear that the module mirrors and displays server-authoritative world state
- **AND** it is not presented as a local gameplay rule authority

### Requirement: ClientWorld Migration Preserves Server Authority
The client world folder migration SHALL preserve the existing server-authoritative movement and sync behavior.

#### Scenario: Directory migration does not reintroduce local movement authority
- **WHEN** the client world runner advances a tick
- **THEN** it does not locally resolve movement rules through a legacy movement resolver
- **AND** player coordinate changes still come from server response, snapshot, or delta application

#### Scenario: Unity references survive migration
- **WHEN** Unity refreshes scripts after the folder migration
- **THEN** `ClientWorldRunner`, networking submitter, view, debug, and test scripts remain compilable
- **AND** EditMode tests can run through Unity TestFramework without requiring a Unity Player build
