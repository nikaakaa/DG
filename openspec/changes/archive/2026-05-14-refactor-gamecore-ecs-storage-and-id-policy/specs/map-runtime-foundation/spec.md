## ADDED Requirements

### Requirement: Spatial Index And ECS Storage Separation
地图运行时 SHALL keep map chunk/cell spatial indexing separate from ECS component storage. Map chunks SHALL continue to represent world-space regions, while ECS storage chunks or archetype groups SHALL represent component-layout groups if or when they are introduced.

#### Scenario: 空间 chunk 仍按地图坐标工作
- **WHEN** an entity is registered, moved, queried, or removed by world coordinate
- **THEN** the spatial index uses cell/chunk keys derived from map coordinates
- **AND** this behavior does not depend on the entity's archetype storage chunk

#### Scenario: 空间查询对接 entity location
- **WHEN** a spatial query returns candidate entity ids for a cell, chunk, or range
- **THEN** component data access goes through the world/entity storage boundary
- **AND** the spatial index does not need to know whether component storage is dictionary-backed, array-backed, or archetype-backed

#### Scenario: 空间索引不承担组件查询
- **WHEN** a rule needs entities with a component combination inside a spatial range
- **THEN** spatial indexing supplies location candidates
- **AND** component/query filtering is handled by the GameCore storage or query boundary
- **AND** no duplicate ad hoc coordinate dictionary is introduced for the same occupancy truth
