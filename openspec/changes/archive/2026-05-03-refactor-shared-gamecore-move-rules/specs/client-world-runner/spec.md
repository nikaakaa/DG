## MODIFIED Requirements
### Requirement: 客户端移动通知接收
客户端 SHALL 通过 Fantasy push handler 接收服务端统一 WorldSnapshot/WorldDelta，并把服务端最终状态应用到当前客户端 world。客户端 MUST NOT 通过本地规则推导玩家、远端玩家或自动移动球的权威坐标。

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

## ADDED Requirements
### Requirement: Unity 客户端显示多个玩家
Unity 客户端 SHALL 能在同一个客户端 world 中同时存在并显示本地 player entity、远端 player entity、自动移动球和阻挡体。

#### Scenario: 收到远端玩家 snapshot
- **WHEN** 客户端收到一个不属于本地玩家的 player entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该远端 player entity

#### Scenario: 收到远端玩家移动通知
- **WHEN** 客户端收到远端 player entity 的服务端最终状态
- **THEN** 客户端把该远端 player entity 更新到服务端最终坐标
- **AND** 客户端不执行本地移动规则来推导远端坐标

#### Scenario: 收到自动移动球 snapshot
- **WHEN** 客户端收到自动移动球 entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该球 entity
- **AND** 显示层能根据组件/tag 或配置标识把它显示为球

#### Scenario: 收到阻挡体 snapshot
- **WHEN** 客户端收到阻挡体 entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该阻挡体 entity
- **AND** 显示层能根据组件/tag 或配置标识显示阻挡体
