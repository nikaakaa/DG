## ADDED Requirements

### Requirement: 服务端权威旋转推动同步
服务端 SHALL 在权威 tick 中执行 rotate-pivot connected body 的 push 响应，并通过统一 `WorldDelta` 同步最终坐标、方向和表现元数据。旋转成功时，`WorldDelta` MUST include every moved member of the rotating connected body for the same server tick，并且 MUST 为每个 connected body 成员写入 rotate-pivot 表现元数据，包括 pivot 成员自身。旋转遇阻并只产生 delayed push output 时，服务端 MUST NOT broadcast changed entity snapshots for the rotating body solely because it transmitted push, but MUST provide rotate bounce and blocker impact metadata sufficient for clients to present the server result. Deferred push and metadata MUST preserve the precise rotate impact context so clients can present the rotating member moving into the blocker and returning when the rotate movement is cancelled. Rotate-pivot presentation metadata MUST use stable style keys / motion ids and MUST NOT require a new gameplay-extension enum member or action-name presentation branch.

#### Scenario: 旋转成功同步全体移动成员
- **WHEN** 服务端权威 tick 中一个 rotate-pivot connected body 被 push 驱动并成功旋转
- **THEN** 服务端 `WorldDelta.ChangedEntities` 包含每个实际移动成员的最终坐标和方向
- **AND** `WorldDelta.AnimationMetadata` 包含每个 connected body 成员的一条 `RotatePivot` metadata
- **AND** pivot 成员也有 `RotatePivot` metadata，坐标 from/to 相同但方向随整体旋转更新
- **AND** 每条 metadata includes pivot entity or pivot coord, from coord, to coord, rotate direction, tick, and stable style key
- **AND** observer clients receive the same full-member delta and metadata for that server tick

#### Scenario: 旋转遇阻只同步反馈
- **WHEN** 服务端权威 tick 中一个 rotate-pivot connected body 被 push 驱动但因外部 blocker 取消自身旋转
- **AND** it emits deferred push output
- **THEN** 服务端 `WorldDelta.ChangedEntities` 不因为该 rotating body 传递 push 而包含它的未移动成员
- **AND** `WorldDelta.AnimationMetadata` contains one `RotatePivotBounce` metadata entry for each rotating body member
- **AND** bounce metadata identifies pivot, from coord, intended impact or arc target, rotate direction, blocker context, and stable style key
- **AND** each affected blocker subject receives same-tick impact feedback metadata in the concrete impact member's rotate tangent direction
- **AND** downstream delayed push keeps using the ordinary push pipeline and may later produce ordinary push animation
- **AND** observer clients receive the same metadata-only result when no entity snapshot changed

#### Scenario: Fantasy 协议保留旋转表现字段
- **WHEN** rotate-pivot 表现需要跨 Fantasy 网络同步
- **THEN** protocol fields for pivot/from/to/impact/rotate direction/bounce/style are appended to the existing animation metadata contract
- **AND** server and Unity client protocol code are generated through the Fantasy protocol export tool
- **AND** generated protocol files are not manually maintained
- **AND** old animation metadata semantics remain compatible for non-rotate motion kinds

#### Scenario: 旋转表现不新增玩法 enum
- **WHEN** rotate-pivot response needs client presentation metadata
- **THEN** the server resolves configured presentation ids at the metadata boundary
- **AND** it does not add a rotate-pivot gameplay enum member to ordinary rule arbitration
- **AND** it does not choose presentation by comparing action names such as `rotate_pivot`

#### Scenario: 旋转表现样式可配置
- **WHEN** the server emits rotate-pivot presentation metadata
- **THEN** success rotation uses the stable style key `rotate_pivot`
- **AND** blocked bounce uses the stable style key `rotate_pivot_bounce`
- **AND** blocker impact feedback uses the stable style key `rotate_pivot_impact`
- **AND** duration, easing, flash, scale, and curve selection remain presentation configuration or strategy concerns rather than authoritative rule outcomes
