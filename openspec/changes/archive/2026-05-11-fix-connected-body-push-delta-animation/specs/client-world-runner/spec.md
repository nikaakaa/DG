## ADDED Requirements
### Requirement: 客户端连接体推动整体动画表现
Unity 客户端 SHALL treat server `WorldDelta` as the only authority for which connected body members moved. When the server delta contains multiple moved connected body members, the client animation layer MUST generate and play animation for every received moved member. The client MUST NOT infer missing moved members from local port graph or local push rules.

#### Scenario: 服务端全员 delta 生成全员动画
- **WHEN** 客户端收到一个 `G2C_WorldDeltaNotify`
- **AND** notify contains changed entity snapshots for every moved member of a connected body
- **AND** notify contains animation metadata for those moved members
- **THEN** `ClientAnimationLayer` creates one animation event per moved member
- **AND** `ClientWorldVisuals` can keep active animations for all moved members at the same time

#### Scenario: 缺失成员不由客户端补齐
- **WHEN** 客户端收到的 `G2C_WorldDeltaNotify` only contains part of a connected body
- **THEN** the client applies only the authoritative snapshots present in the delta
- **AND** the client does not use local port graph, local push rules, or cached connected body membership to move additional members
- **AND** the missing-member condition is treated as a server sync correctness problem rather than a client-side rule decision

#### Scenario: metadata-only 推力反馈不改变坐标
- **WHEN** 客户端收到一个 `G2C_WorldDeltaNotify`
- **AND** notify contains no changed entity snapshots
- **AND** notify contains mechanism push animation metadata for one or more entities
- **THEN** `ClientAnimationLayer` creates impulse animation events for those entities
- **AND** `ClientWorldVisuals` plays push feedback for those entities
- **AND** the client does not change any entity coordinate because of that metadata-only delta

#### Scenario: 连续 delta 后全员落到最新服务端状态
- **WHEN** the client receives consecutive server deltas for multiple connected body members
- **THEN** each moved member eventually reaches its latest authoritative coordinate
- **AND** later deltas replace older active animations per entity without losing other members' animations
