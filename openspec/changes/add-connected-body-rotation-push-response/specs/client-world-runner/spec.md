## ADDED Requirements

### Requirement: 客户端旋转推动表现播放
Unity 客户端 SHALL consume server-authored `WorldDelta.AnimationMetadata` for rotate-pivot presentation and MUST NOT infer rotate arcs, blockers, or bounce intent from final snapshots alone. The client SHALL preserve the server metadata fields through network conversion, produce deterministic animation events, and settle visuals to the authoritative snapshot after playback.

#### Scenario: 客户端保留服务端旋转字段
- **WHEN** Unity client receives `WorldDelta.AnimationMetadata` with rotate-pivot fields
- **THEN** network conversion preserves motion kind, style key, pivot, from coord, to coord, impact coord, rotate direction, blocker context, and bounce flag
- **AND** `RotatePivot` and `RotatePivotBounce` become explicit client animation events
- **AND** missing optional impact fields are handled without guessing gameplay rules on the client

#### Scenario: 成功旋转按圆弧播放
- **WHEN** a connected body receives same-tick `RotatePivot` metadata for all members
- **THEN** the client plays each non-pivot member along the arc around the shared pivot rather than using linear interpolation from coord to coord
- **AND** the pivot member rotates its facing or port visual in place
- **AND** members use the same tick, pivot, rotate direction, duration, and curve strategy so the body appears visually rigid
- **AND** playback completion matches the authoritative final snapshot

#### Scenario: 遇阻旋转按反弹播放
- **WHEN** a connected body receives `RotatePivotBounce` metadata after blocked rotation
- **THEN** the client plays the rotating members along the intended arc toward the impact side and then returns them to their authoritative original coords
- **AND** the pivot member participates in the same bounce timing and direction presentation
- **AND** playback completion leaves the body at the unchanged authoritative snapshot

#### Scenario: blocker 同 tick 冲击反馈
- **WHEN** blocked rotate-pivot metadata identifies one or more impacted blocker subjects
- **THEN** the client plays blocker impact feedback in the tangent direction of the concrete impact member
- **AND** this impact feedback is same-tick presentation metadata rather than a client-side collision decision
- **AND** if a later delayed push succeeds, that later movement uses the ordinary push animation path

#### Scenario: 旋转动画样式可配置
- **WHEN** a rotate-pivot animation event is created
- **THEN** `rotate_pivot`, `rotate_pivot_bounce`, and `rotate_pivot_impact` style keys resolve duration and visual parameters through animation configuration
- **AND** curve evaluation may live inside each animation strategy
- **AND** changing presentation curves does not change authoritative grid coordinates, directions, or blocker decisions

#### Scenario: 连续 tick 表现保持确定性
- **WHEN** an entity receives a new authoritative snapshot or animation event while an older rotate-pivot animation is still playing
- **THEN** the client resolves playback by tick/order using existing animation replacement or settlement rules
- **AND** the final visual state always converges to the newest authoritative snapshot
- **AND** no client-only physics collision query is required to decide whether the rotation happened
