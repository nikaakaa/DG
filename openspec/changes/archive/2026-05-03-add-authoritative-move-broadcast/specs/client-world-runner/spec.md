## ADDED Requirements
### Requirement: 客户端 observer 注册
客户端 SHALL 在双客户端同步验证前通过最小注册请求进入服务端 observer 集合，而不是依赖先发送移动请求才被服务端发现。

#### Scenario: 连接后注册 observer
- **WHEN** 客户端 Fantasy session 可用
- **AND** demo entity id 已确定
- **THEN** 客户端发送 observer 注册请求
- **AND** 注册成功后客户端可以接收其他客户端触发的 `G2C_EntityMovedNotify`

#### Scenario: B 未移动也能接收广播
- **WHEN** 客户端 B 已完成 observer 注册
- **AND** B 尚未发送 `C2G_MoveRequest`
- **THEN** 客户端 B 仍能接收 A 成功移动产生的 `G2C_EntityMovedNotify`

#### Scenario: 注册不创建第二个 world
- **WHEN** 客户端处理 observer 注册响应
- **THEN** 客户端复用当前 `ClientWorldRunner` 和 `World`
- **AND** 客户端不创建第二个独立 `World`

### Requirement: 客户端移动通知接收
客户端 SHALL 通过 Fantasy push handler 接收 `G2C_EntityMovedNotify`，并把服务端最终坐标应用到当前客户端 world。

#### Scenario: 接收移动通知更新本地 world
- **WHEN** 客户端收到 `G2C_EntityMovedNotify`
- **AND** 通知中的 entity id 在当前 `World` 中存在
- **THEN** 客户端将该 entity 更新到通知中的 `FinalX` 和 `FinalY`
- **AND** 更新进入现有 dirty flush 链路

#### Scenario: 缺失 entity 的通知不打断客户端
- **WHEN** 客户端收到 `G2C_EntityMovedNotify`
- **AND** 通知中的 entity id 在当前 `World` 中不存在
- **THEN** 客户端不抛异常
- **AND** 客户端记录可验证日志

#### Scenario: 重复最终坐标保持幂等
- **WHEN** 客户端已位于 notify 的最终坐标
- **AND** 再次收到相同 entity id 和最终坐标的 `G2C_EntityMovedNotify`
- **THEN** 客户端坐标保持不变
- **AND** 不产生错误的额外移动结果

### Requirement: 双客户端移动同步
客户端 SHALL 支持两个已连接客户端通过服务端广播同步同一 entity 的权威移动结果。

#### Scenario: A 移动后 B 应用服务端通知
- **WHEN** 客户端 A 发起合法移动
- **AND** 服务端广播 `G2C_EntityMovedNotify`
- **AND** 客户端 B 收到该通知
- **THEN** 客户端 B 将同一 entity 坐标更新为服务端最终坐标

#### Scenario: 发起者同时处理响应和通知
- **WHEN** 客户端 A 发起合法移动
- **AND** 客户端 A 收到 `G2C_MoveResponse Success=true`
- **AND** 客户端 A 收到 `G2C_EntityMovedNotify`
- **THEN** 客户端 A 最终坐标与服务端最终坐标一致
- **AND** 重复应用不会把坐标推到其他位置

#### Scenario: 非法移动不影响 B
- **WHEN** 客户端 A 发起非法移动
- **AND** 服务端回复 A 的 `G2C_MoveResponse Success=false`
- **THEN** 客户端 B 不应用新的 `G2C_EntityMovedNotify`
- **AND** 客户端 B 上该 entity 坐标保持为非法移动前的服务端确认坐标

### Requirement: Minestom 广播参考边界
客户端 SHALL 只接入服务端推送的最小移动结果，不移植 Minestom 的 AOI、packet 或线程模型。

#### Scenario: 使用 observer/session 集合替代完整 AOI
- **WHEN** 客户端参与第一版双客户端同步
- **THEN** 客户端只期望收到在线集合广播的 `G2C_EntityMovedNotify`
- **AND** 不要求客户端维护 chunk view distance 或 add/remove 可见性差量

#### Scenario: 不引入客户端预测或插值
- **WHEN** 客户端处理 `G2C_EntityMovedNotify`
- **THEN** 客户端直接应用服务端最终坐标
- **AND** 不在本变更中实现预测、回滚或插值
