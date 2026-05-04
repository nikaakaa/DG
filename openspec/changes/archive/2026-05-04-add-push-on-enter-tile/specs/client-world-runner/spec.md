## ADDED Requirements
### Requirement: 客户端显示进入推动地格
Unity 客户端 SHALL 能通过服务端 snapshot/delta 创建或更新传送带这类进入推动地格的镜像 entity，并使用配置标识或 tag 选择最小显示方式。

#### Scenario: 收到传送带 snapshot
- **WHEN** 客户端收到传送带 entity snapshot
- **THEN** 客户端在当前 world 中创建或更新该 mirror entity
- **AND** 该 mirror entity 的坐标来自服务端 snapshot
- **AND** 客户端显示层能把它和玩家、球、阻挡体区分开

#### Scenario: 客户端不本地裁决推动
- **WHEN** 本地玩家站上传送带显示格
- **THEN** 客户端不直接修改权威玩家坐标
- **AND** 客户端等待服务端 WorldDelta 应用最终坐标
