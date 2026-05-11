## MODIFIED Requirements
### Requirement: 服务端进入推动 tick
服务端 SHALL 在权威 tick 中执行进入推动规则，并在推动产生状态变化或推力反馈后通过统一 WorldDelta 同步给在线客户端。当进入推动或 handoff 推动的目标是 connected body subject 时，服务端 MUST 在同一次成功移动结果中同步每个实际移动成员的最终状态和动画元数据。普通 push 链中间 subject 继续传递 push 但自身不移动时，服务端 MUST broadcast metadata-only WorldDelta so clients can play push feedback without changing authoritative coordinates.

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
- **AND** `WorldDelta.AnimationMetadata` MUST contain a movement metadata entry for every moved member
- **AND** observer clients MUST receive the same full-member `WorldDelta`

#### Scenario: 中间 subject 推力反馈同步
- **WHEN** a connected body or single entity receives push and continues handoff to a downstream subject
- **AND** that intermediate subject does not move during the current authoritative tick
- **THEN** 服务端 `WorldDelta.ChangedEntities` MUST NOT include that intermediate subject solely because it transmitted push
- **AND** `WorldDelta.AnimationMetadata` MUST contain mechanism push feedback metadata for the intermediate subject members
- **AND** observer clients MUST receive the metadata-only delta when no entity snapshot changed
