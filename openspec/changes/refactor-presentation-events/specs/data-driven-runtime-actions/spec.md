## ADDED Requirements

### Requirement: 表现事件使用字符串 ID
系统 SHALL 使用可扩展字符串 ID 表达表现事件语义和表现入口。`PresentationEvent.KindId` and `PresentationEvent.CueId` MUST be strings or strongly typed string value objects. The system MUST NOT require adding a shared presentation enum or client motion enum member for each new ordinary presentation event.

#### Scenario: 新表现不新增枚举
- **WHEN** 新增一个普通表现事件，例如 `trap.triggered` 或 `skill.cast`
- **THEN** 开发者新增配置和必要客户端 cue player
- **AND** 不需要修改共享表现枚举、协议 motion enum 或客户端 motion enum

#### Scenario: kind 和 cue 分离
- **WHEN** 两个动作都产生 `entity.pushed`
- **THEN** 它们可以使用不同 `cueId`
- **AND** 客户端通过 cue 配置决定具体动画、VFX、SFX 或组合表现

### Requirement: 表现配置数据驱动
系统 SHALL 通过配置或 registry 将动作结果映射到 `PresentationEvent`。普通 action 的表现映射 MUST NOT be selected by comparing raw action spec names in authoritative tick orchestration. `ActionSpec` MAY reference presentation profile data, or an action presentation registry MAY map resolved action ids to kind/cue data.

#### Scenario: 普通动作表现来自配置
- **WHEN** `player_move`、`mechanism_push` 或后续普通 action 成功执行
- **THEN** 表现 composer 从配置或 registry 获取 kind/cue
- **AND** 权威 tick runner 不新增 action-name 表现分支

#### Scenario: 新 cue 只改配置和客户端表现
- **WHEN** 一个已存在逻辑行为需要更换表现 cue
- **THEN** 修改表现配置或客户端 cue 配置即可
- **AND** 不需要修改 action arbitration、planning、commit 或 tick runner

### Requirement: 表现 payload 边界
`PresentationEvent.PayloadJson` SHALL be reserved for presentation-only parameters that cannot be expressed by common event fields. Payload data MUST NOT affect authoritative world state, arbitration, planning, commit, targeting, conflict, or component/tag settlement.

#### Scenario: 通用字段不重复进入 payload
- **WHEN** 表现事件已有 source entity、subject ids、from/to、direction、server tick 或 cue id
- **THEN** payload 不重复保存这些通用字段
- **AND** payload 只保存事件专属表现参数

#### Scenario: pivot 参数进入 payload
- **WHEN** rotate pivot 表现需要 pivot entity、pivot coord、rotate direction、bounce 或 impact coord
- **THEN** 这些专属表现参数可以进入 payload
- **AND** 权威旋转结果仍由 WorldDelta entity snapshots 表达

### Requirement: 表现事件验证
系统 SHALL include Unity TestFramework EditMode tests and server verification for presentation event generation and client consumption. Manual Play Mode verification SHALL confirm server-authoritative end-to-end state convergence and presentation consistency across two clients.

#### Scenario: 自动验证表现边界
- **WHEN** automated verification runs
- **THEN** it includes Unity TestFramework EditMode tests proving presentation events do not mutate authoritative client world state
- **AND** server verification proves player move, mechanism push, rotate pivot success, and rotate pivot blocked cases produce stable presentation events

#### Scenario: 手动端到端验证
- **WHEN** 用户在 Play Mode 中运行服务端权威路径和两个客户端
- **AND** 玩家移动、机关推动或 rotate pivot 产生服务端状态变化或表现反馈
- **THEN** 两个客户端通过同一 WorldDelta 收敛到一致权威状态
- **AND** 两个客户端播放一致 cue 表现
