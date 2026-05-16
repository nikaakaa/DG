## MODIFIED Requirements

### Requirement: 客户端移动通知接收
客户端 SHALL 通过 Fantasy push handler 接收服务端统一 WorldSnapshot/WorldDelta，并把服务端最终状态应用到当前客户端 world。客户端 MUST NOT 通过本地规则推导玩家、远端玩家、自动移动球或表现事件的权威坐标。客户端 MAY consume `PresentationEvents` from the same WorldDelta to play feedback, but those events MUST NOT decide authoritative world state.

#### Scenario: 接收移动通知更新本地 world
- **WHEN** 客户端收到服务端同步的 entity 最终状态
- **AND** 通知中的 entity id 在当前 `World` 中存在
- **THEN** 客户端将该 entity 更新到服务端最终坐标
- **AND** 更新进入现有 dirty flush 链路

#### Scenario: 缺失 entity 的通知不打断客户端
- **WHEN** 客户端收到服务端同步的 entity 最终状态
- **AND** 通知中的 entity id 在当前 `World` 中不存在
- **THEN** 客户端可以根据 snapshot/delta 创建镜像 entity 或记录可验证日志
- **AND** 客户端不抛异常

#### Scenario: 重复最终坐标保持幂等
- **WHEN** 客户端已位于同步消息的最终坐标
- **AND** 再次收到相同 entity id 和最终坐标的同步消息
- **THEN** 客户端坐标保持不变
- **AND** 不产生错误的额外移动结果

#### Scenario: 自动移动球不由客户端推进
- **WHEN** 客户端尚未收到下一次服务端 world delta
- **AND** 本地镜像 entity 拥有自动移动或方向状态
- **THEN** 客户端不使用本地规则推进该 entity 的权威坐标
- **AND** 该 entity 的下一次权威位置变化来自服务端同步

#### Scenario: 表现事件不写权威状态
- **WHEN** 客户端收到只包含 `PresentationEvents` 且没有 entity snapshot 的 WorldDelta
- **THEN** 客户端可以调度表现反馈
- **AND** `ClientMapWorld` 权威坐标、方向、组件和 tag 不因为表现事件改变

## ADDED Requirements

### Requirement: 客户端表现事件消费
客户端 SHALL 将 `WorldDelta.PresentationEvents` 转换为客户端表现队列事件。表现事件 SHALL be keyed by server tick and event id for idempotent enqueue. Unknown `kindId` or `cueId` MUST NOT break authoritative state application.

#### Scenario: move 表现事件入队
- **WHEN** 客户端收到包含 `kindId=entity.moved` 和有效 `cueId` 的表现事件
- **THEN** 客户端表现层入队对应表现
- **AND** 表现播放读取 cue 配置而不是普通 action 名称

#### Scenario: 未知表现不影响状态
- **WHEN** 客户端收到未知 `kindId` 或缺失 cue 配置
- **THEN** 客户端跳过或记录该表现
- **AND** 同一 WorldDelta 的 entity snapshot 和 removed ids 仍正常应用

#### Scenario: payload 驱动专属表现
- **WHEN** 客户端收到 rotate pivot 表现事件
- **AND** 事件 `payloadJson` 包含 pivot、rotate direction、bounce 或 impact 参数
- **THEN** 客户端表现层用 payload 生成旋转、弹回或冲击表现
- **AND** payload 不被用于修改权威世界状态
