## MODIFIED Requirements

### Requirement: 服务端进入推动 tick
服务端 SHALL 在权威 tick 中执行进入推动规则，并在推动产生状态变化或表现反馈后通过统一 WorldDelta 同步给在线客户端。当进入推动或 handoff 推动的目标是 connected body subject 时，服务端 MUST 在同一次成功移动结果中同步每个实际移动成员的最终状态和表现事件。普通 push 链中间 subject 继续传递 push 但自身不移动时，服务端 MUST broadcast metadata-only WorldDelta so clients can play presentation feedback without changing authoritative coordinates.

#### Scenario: 推动系统参与服务端 tick
- **WHEN** 服务端 tick 推进
- **THEN** 服务端先把自动移动生成 state-driven auto movement action 或 intent
- **AND** 服务端再把进入推动生成 state-driven mechanism push action 或 intent
- **AND** 服务端最后广播包含推动结果的 WorldDelta

#### Scenario: 推动结果同步给观察者
- **WHEN** state-driven mechanism push 成功推动一个 player entity
- **THEN** 服务端 GameWorld 标记该 entity dirty
- **AND** 在线 observer 收到包含该 entity 最终坐标的 WorldDelta
- **AND** 客户端不通过本地传送带规则推导权威坐标

#### Scenario: 连接体推动结果同步全体成员
- **WHEN** 玩家移动、机关推动或进入推动通过 handoff 成功推动一个 connected body
- **THEN** 服务端 `WorldDelta.ChangedEntities` MUST contain every moved member of that connected body
- **AND** each moved member snapshot MUST contain the authoritative final coordinate for the same server tick
- **AND** `WorldDelta.PresentationEvents` MUST contain a presentation event for every moved member or an equivalent grouped presentation event covering the moved members
- **AND** observer clients MUST receive the same full-member `WorldDelta`

#### Scenario: 中间 subject 推力反馈同步
- **WHEN** a connected body or single entity receives push and continues handoff to a downstream subject
- **AND** that intermediate subject does not move during the current authoritative tick
- **THEN** 服务端 `WorldDelta.ChangedEntities` MUST NOT include that intermediate subject solely because it transmitted push
- **AND** `WorldDelta.PresentationEvents` MUST contain push feedback presentation for the intermediate subject members
- **AND** observer clients MUST receive the metadata-only delta when no entity snapshot changed

## ADDED Requirements

### Requirement: 权威表现事件编排边界
服务端 SHALL 在规则执行完成后、WorldDelta 同步发送前，通过独立 `PresentationEventComposer` 或等价模块生成表现事件。表现事件 SHALL 描述领域表现语义，不得改变权威 GameWorld 状态。`AuthoritativeWorldTickRunner` MUST NOT 通过普通 action 名称、style key 或表现枚举内联决定普通表现分支。

#### Scenario: tick runner 调用表现 composer
- **WHEN** `StateDrivenRuleExecutionSystem` 返回规则执行结果
- **THEN** tick runner 将 server tick、ready actions 和 rule result 交给表现 composer
- **AND** composer 返回表现事件集合
- **AND** tick runner 不在自身代码中比较 `player_move`、`mechanism_push`、`auto_move` 或其他普通 action 名称来选择表现

#### Scenario: 表现事件不修改世界状态
- **WHEN** composer 生成 `entity.pushed`、`pivot.rotated` 或 `action.blocked` 表现事件
- **THEN** GameWorld 坐标、方向、组件和 tag 的最终事实只来自规则 commit
- **AND** 表现事件只进入 `WorldDelta.PresentationEvents`

### Requirement: WorldDelta 携带表现事件
服务端 SHALL 继续使用统一 `WorldDelta` 同步最终权威状态，并在同一 delta 中携带表现事件。表现事件 MUST share the same server tick alignment as the delta that carries them. 系统 MUST NOT require an independent presentation notify channel for this change.

#### Scenario: 状态和表现同 tick 广播
- **WHEN** 一个服务端 tick 产生实体移动和对应表现
- **THEN** `G2C_WorldDeltaNotify` 包含实体最终 snapshot
- **AND** 同一个 notify 包含对应 `PresentationEvents`
- **AND** 客户端可以先应用权威状态再调度表现

#### Scenario: 只有表现也广播
- **WHEN** 一个 tick 只产生表现反馈而没有实体 snapshot 或删除
- **THEN** 服务端仍可广播包含 `PresentationEvents` 的 WorldDelta
- **AND** 该 delta 不改变客户端权威世界状态
