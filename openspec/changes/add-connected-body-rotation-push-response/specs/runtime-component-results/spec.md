## ADDED Requirements
### Requirement: Rotate Pivot Runtime Effect Result
系统 SHALL allow `RotatePivotComponent` to be produced as a final component result from static component sources and RuntimeEffect / buff sources. Runtime pivot effects MUST use the existing EffectSpec duration, stack, remove policy, target binding, commit, store, and ComponentStateResolver boundaries. Rules MUST read only the final `RotatePivotComponent` result and MUST NOT query RuntimeEffect or buff state to decide rotate response.

#### Scenario: runtime effect grants pivot
- **WHEN** a configured RuntimeEffect with rotate-pivot payload is applied to a target entity
- **THEN** commit records a runtime effect source for that target
- **AND** ComponentStateResolver produces final `RotatePivotComponent` for that entity
- **AND** a connected body containing exactly one final pivot may enter rotate response

#### Scenario: runtime pivot removal preserves static pivot
- **WHEN** an entity has static `RotatePivotComponent`
- **AND** a RuntimeEffect also contributes rotate-pivot payload to that entity
- **AND** that RuntimeEffect expires or is removed
- **THEN** the entity still has final `RotatePivotComponent`

#### Scenario: last runtime pivot source removal clears runtime-only pivot
- **WHEN** an entity has no static `RotatePivotComponent`
- **AND** runtime effect A and runtime effect B both contribute rotate-pivot payload
- **AND** runtime effect A is removed
- **THEN** the entity still has final `RotatePivotComponent`
- **WHEN** runtime effect B is removed or expires
- **THEN** final `RotatePivotComponent` is removed

#### Scenario: rules do not inspect effect state
- **WHEN** a push targets a connected body that has one pivot from runtime effect resolution
- **THEN** rotate response reads the final `RotatePivotComponent`
- **AND** it does not inspect `EffectSpec`, `RuntimeEffectStore`, buff ids, readable effect names, or action names to decide pivot behavior
