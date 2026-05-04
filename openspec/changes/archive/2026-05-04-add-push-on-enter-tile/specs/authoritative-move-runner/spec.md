## ADDED Requirements
### Requirement: 服务端进入推动 tick
服务端 SHALL 在权威 tick 中执行进入推动规则，并在推动产生状态变化后通过统一 WorldDelta 同步给在线客户端。

#### Scenario: 推动系统参与服务端 tick
- **WHEN** 服务端 tick 推进
- **THEN** 服务端先执行现有自动移动规则
- **AND** 服务端再执行进入推动规则
- **AND** 服务端最后广播包含推动结果的 WorldDelta

#### Scenario: 推动结果同步给观察者
- **WHEN** `PushOnEnterSystem` 成功推动一个 player entity
- **THEN** 服务端 GameWorld 标记该 entity dirty
- **AND** 在线 observer 收到包含该 entity 最终坐标的 WorldDelta
- **AND** 客户端不通过本地传送带规则推导权威坐标
