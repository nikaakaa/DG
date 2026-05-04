## MODIFIED Requirements
### Requirement: 统一 Dirty 与 WorldSnapshot/WorldDelta
系统 SHALL 用统一 dirty、WorldSnapshot 和 WorldDelta 数据模型表达玩家、球、阻挡体、进入推动地格以及调试编辑实体的服务端权威状态变化。WorldDelta SHALL 同时支持仍然存在的 changed entity snapshot 和已经被移除的 removed entity id。

#### Scenario: 玩家移动产生 delta
- **WHEN** 玩家 MoveCommand 成功移动
- **THEN** GameWorld 标记该玩家 entity 的位置变化
- **AND** 同步层可以从 dirty 中构造该玩家 entity 的 WorldDelta changed entity

#### Scenario: 球自动移动产生 delta
- **WHEN** AutoMoveSystem 驱动球成功移动或反弹
- **THEN** GameWorld 标记该球 entity 的位置或方向变化
- **AND** 同步层可以从 dirty 中构造该球 entity 的 WorldDelta changed entity

#### Scenario: Join 后发送统一 snapshot
- **WHEN** 客户端 JoinWorld 成功
- **THEN** 服务端发送当前 GameWorld 中相关 entity 的 WorldSnapshot
- **AND** WorldSnapshot 可以包含玩家、球、阻挡体和进入推动地格

#### Scenario: 删除实体产生 removed delta
- **WHEN** 服务端从 GameWorld 删除一个已存在 entity
- **THEN** GameWorld 记录该 entity id 为 removed entity id
- **AND** 下一次 FlushDelta 返回的 WorldDelta 包含该 removed entity id
- **AND** 该 removed entity id 不需要对应 changed entity snapshot

#### Scenario: 删除不存在实体不产生 delta
- **WHEN** 服务端请求删除一个 GameWorld 中不存在的 entity id
- **THEN** GameWorld 不记录 removed entity id
- **AND** 下一次 FlushDelta 不因为该请求产生 WorldDelta 内容

#### Scenario: 同一 delta 中删除和变更可区分
- **WHEN** 同一服务端 tick 中既有实体状态变更也有实体删除
- **THEN** WorldDelta 分别暴露 changed entity snapshots 和 removed entity ids
- **AND** 消费方不需要通过缺失 snapshot 推断实体删除
