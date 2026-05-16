## ADDED Requirements
### Requirement: Shared GameCore Source Layout Semantics
Shared GameCore SHALL organize source files by stable gameplay and runtime semantics rather than by incidental implementation history. The layout MUST keep pure domain data, configuration import, rule execution, world storage, spatial indexing, snapshot/delta contracts, runtime effects, and test-only helpers discoverable as separate responsibilities.

#### Scenario: Developer finds the rule boundary
- **WHEN** a developer needs to modify action intake, targeting, arbitration, execution strategy, planning, commit, connectivity, or deferred output behavior
- **THEN** the relevant source files are located under a `Rules` subdirectory whose folder name identifies that responsibility
- **AND** the developer does not need to inspect config provider, Unity, Fantasy, generated Luban, or storage adapter folders to find ordinary rule code

#### Scenario: Domain and runtime shell stay separated
- **WHEN** Shared GameCore source files are moved or split
- **THEN** files under Shared GameCore still compile without `Fantasy`, `Session`, protocol generated types, `UnityEngine`, `MonoBehaviour`, or `GameObject`
- **AND** the move does not create a new runtime-shell dependency

#### Scenario: Generated configuration stays isolated
- **WHEN** Luban generated C# files exist in Shared GameCore
- **THEN** they remain under an explicit generated configuration subtree
- **AND** rule modules consume runtime provider or registry abstractions instead of generated table classes

### Requirement: Shared Rule File Split Preserves Behavior
Shared GameCore file splitting SHALL preserve existing public rule behavior, storage boundary behavior, snapshot/delta semantics, and runtime effect final component semantics. Splitting a large file into semantic files MUST NOT introduce a new gameplay branch, new action policy, or new storage backend behavior.

#### Scenario: Action pipeline split is semantic only
- **WHEN** `ActionPipeline` responsibilities are split into targeting, claim, blocked outcome, strategy registration, and concrete strategy files
- **THEN** existing action requests produce the same accepted claims, rejected claims, commits, deferred outputs, and result metadata for the same world state
- **AND** action behavior is still selected by `ActionSpec` policy and registered strategies rather than action name strings

#### Scenario: GameWorld remains the public boundary
- **WHEN** world storage or query files are moved under a clearer source layout
- **THEN** external rule modules, server Hotfix code, Unity mirror code, and protocol mapping code still use `GameWorld` public APIs
- **AND** they do not directly reference storage adapter internals, component pool internals, or third-party ECS types

#### Scenario: Automated semantic regression
- **WHEN** Unity TestFramework EditMode tests run after the layout migration
- **THEN** tests cover representative action targeting, strategy execution, claim arbitration, commit, runtime effect final component resolution, snapshot/delta, and spatial query behavior
- **AND** those tests assert behavior and world state rather than the physical file path of implementation classes
