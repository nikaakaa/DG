## ADDED Requirements

### Requirement: GameCore Runtime ID Boundary
Shared GameCore SHALL convert configuration-readable action, policy, style, and tag names into stable runtime identifiers before authoritative rules consume them. Runtime arbitration, planning, commit, pending, deferred output, and component result resolution MUST consume typed or numeric identifiers and final component/tag values rather than comparing raw strings.

#### Scenario: 配置名在导入边界解析
- **WHEN** Luban action, blocked policy, push-on-enter output, entity tag, or animation style data is loaded
- **THEN** the provider or registry resolves readable names into runtime identifiers or enums
- **AND** rule execution receives the resolved identifier or enum value
- **AND** the readable name remains available only for authoring, diagnostics, or presentation metadata

#### Scenario: 规则层不比较字符串 spec
- **WHEN** player move, auto move, mechanism push, configured wind push, debug action, or push handoff enters arbitration
- **THEN** subject selection, blocked result, handoff, priority, conflict, merge, planning, and commit decisions are selected from typed policy data
- **AND** the rules layer does not compare raw action spec strings such as `player_move`, `mechanism_push`, `auto_move`, or future ordinary behavior names

#### Scenario: tag 作为位标记进入规则层
- **WHEN** action policy or entity archetype data contains source, ability, state, immunity, or blocker tags
- **THEN** configuration import resolves those tags to `WorldTag` or an equivalent typed tag set
- **AND** authoritative rules read final tag/component facts rather than raw string tag names

### Requirement: ECS Storage Abstraction Boundary
Shared GameCore SHALL expose world, entity, component, query, spatial, dirty, snapshot, and delta semantics through stable APIs that do not reveal whether component data is stored in dictionaries, arrays, or archetype chunks. External rule modules MUST NOT depend on dictionary key order, per-query array allocation, or concrete `ComponentStore<T>` internals.

#### Scenario: 外部系统不依赖字典顺序
- **WHEN** a rule module needs to enumerate entities
- **THEN** it uses an API whose ordering contract is explicit
- **AND** unordered iteration is not treated as entity id sorted order
- **AND** sorted iteration is requested only when deterministic output requires it

#### Scenario: 查询语义不依赖内部存储
- **WHEN** a rule module queries entities with position, collider, auto move, push-on-enter, or port connector data
- **THEN** it uses a GameCore query or subset boundary
- **AND** the result is valid whether the internal storage is dictionary-backed or archetype-backed
- **AND** tests assert behavior and world state rather than internal chunk or dictionary layout

#### Scenario: snapshot/delta 语义保持
- **WHEN** GameCore changes its internal component storage implementation
- **THEN** `EntitySnapshot`, `WorldDelta`, dirty entity ids, changed cells, changed chunks, and removed entity ids preserve their existing external semantics
- **AND** server-authoritative sync consumers do not need to know the internal storage layout

### Requirement: Runtime Storage Observability
Shared GameCore SHALL provide a way to observe hot path storage and query costs before and during ECS storage migration. The observation surface MUST distinguish rule execution cost from debug logging, snapshot construction, spatial queries, and component lookup counts.

#### Scenario: 热路径统计可读
- **WHEN** automated tests or diagnostics run a server-authoritative tick
- **THEN** GameCore can report counts for entity enumeration, component lookup, spatial query, and snapshot/delta construction paths
- **AND** those counts can be disabled or ignored in normal gameplay

#### Scenario: 迁移收益需要证据
- **WHEN** an implementation claims a storage-layer optimization or archetype migration
- **THEN** it includes automated semantic regression tests
- **AND** it includes before/after observation for the relevant hot path
- **AND** compile success alone is not considered proof of migration safety
