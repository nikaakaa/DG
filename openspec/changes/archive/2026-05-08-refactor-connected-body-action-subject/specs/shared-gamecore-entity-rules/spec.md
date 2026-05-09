## ADDED Requirements
### Requirement: Port Graph Composition Boundary
系统 SHALL treat port as an entity connection capability, not as an entity hierarchy. Runtime port connectivity MUST be resolved as graph connectivity and exposed to rules as a flat connected body view.

#### Scenario: port 表达连接能力
- **WHEN** an entity has final `PortConnectorComponent`
- **THEN** the component describes available local ports and compatibility-relevant connection capability
- **AND** it does not describe parent, child, control ownership, action waiting, or action lifecycle relationships

#### Scenario: 图连通块形成 connected body view
- **WHEN** port-compatible entities are adjacent through matching world ports
- **THEN** the connection system resolves a connected component graph
- **AND** the rule layer receives a flat connected body view with a stable root and members
- **AND** the root is a representative, not a parent entity

#### Scenario: 环形连接不需要树结构
- **WHEN** port connections form a cycle
- **THEN** the connection system still resolves one connected body view
- **AND** rule evaluation does not require choosing parent-child relationships between members

### Requirement: Composite Body Entity Deferral
系统 SHALL NOT introduce a persistent `CompositeBodyEntity` for basic port-connected movement. A persistent composite body entity MAY be proposed later only when body-level long-lived state is required.

#### Scenario: 临时 connected body 不创建持久组合体
- **WHEN** entities are connected only to move, collide, or be evaluated together for the current tick
- **THEN** the system uses connected body view
- **AND** it does not create a persistent `CompositeBodyEntity`

#### Scenario: 长期 body 状态需要新 proposal
- **WHEN** future behavior needs body-level hp、energy、inventory、owner、save/load identity, or state split policy
- **THEN** the persistent `CompositeBodyEntity` design requires a separate OpenSpec proposal
- **AND** that proposal must define how state survives attach, detach, split, merge, and sync
