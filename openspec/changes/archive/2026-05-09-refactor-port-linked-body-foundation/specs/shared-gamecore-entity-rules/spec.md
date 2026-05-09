## MODIFIED Requirements
### Requirement: Port Graph Composition Boundary
系统 SHALL treat port as an entity connection capability, not as an entity hierarchy. Runtime port connectivity MUST be resolved as graph connectivity and exposed to rules as a flat connected body view. Port connectivity MUST NOT automatically propagate member components across the connected body.

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

#### Scenario: linked body 不传播 component
- **WHEN** entity A has `PushableComponent`
- **AND** entity B does not have `PushableComponent`
- **AND** A and B are connected through matching ports
- **THEN** the connected body view includes both A and B
- **AND** B does not gain `PushableComponent`
- **AND** snapshots and deltas still report each member's own final components

## ADDED Requirements
### Requirement: Port Linked Body Capability Resolution
系统 SHALL resolve linked body behavior through body-level capability resolution. `PushableComponent` SHALL be treated as a push entry capability on the touched member, while body movement SHALL be decided from the connected body view and final member components.

#### Scenario: pushable entry moves linked body
- **WHEN** entity A has `PushableComponent`
- **AND** entity B does not have `PushableComponent`
- **AND** A and B are connected through matching ports
- **AND** an external action pushes A in a valid direction
- **THEN** A is accepted as the push entry
- **AND** the system resolves A-B as one connected body subject
- **AND** planning creates movement claims for all moved body members
- **AND** commit moves A and B together only if every required claim is valid

#### Scenario: non-pushable entry rejects linked body push
- **WHEN** entity A has `PushableComponent`
- **AND** entity B does not have `PushableComponent`
- **AND** A and B are connected through matching ports
- **AND** an external action pushes B
- **THEN** B is not accepted as a push entry
- **AND** the system does not create a linked body push action from B
- **AND** A and B remain at their original coordinates

#### Scenario: member movement permission blocks body movement
- **WHEN** a linked body contains multiple members
- **AND** any member has final movement permission that forbids movement
- **AND** a push entry on that body is otherwise valid
- **THEN** body movement is rejected
- **AND** no member position is committed

#### Scenario: linked body commit is all-or-nothing
- **WHEN** a linked body move action is accepted for planning
- **AND** any required member claim is blocked by an external blocker or invalid target
- **THEN** the action unit fails or waits according to policy
- **AND** no partial member movement is reported as success

#### Scenario: capability resolver 不依赖 port graph 扩散能力
- **WHEN** rules evaluate push entry, movement permission, blocking, or control for a linked body
- **THEN** the rule layer uses final member components and body capability resolution
- **AND** the port graph only supplies connectivity and member membership

### Requirement: Port Graph Cache Boundary
系统 MAY cache port graph and connected body views for performance, but cache state MUST be rebuildable from final components and authoritative world state. Cache invalidation MUST be driven by changes to port connector, position, direction, entity spawn, and entity removal.

#### Scenario: cache 可重建
- **WHEN** a port graph or connected body cache is cleared
- **THEN** the system can rebuild equivalent linked body views from final `PortConnectorComponent`, `PositionComponent`, `DirectionComponent`, and current entity membership

#### Scenario: dirty source 触发重算
- **WHEN** an entity's port connector, position, direction, spawn state, or removal state changes
- **THEN** the affected port graph or connected body view is marked dirty
- **AND** later rule evaluation observes the updated connectivity

#### Scenario: cache 不引入持久组合体身份
- **WHEN** a connected body cache stores body id, root, members, or version
- **THEN** the cached body remains a runtime view
- **AND** it does not become a persistent `CompositeBodyEntity`
