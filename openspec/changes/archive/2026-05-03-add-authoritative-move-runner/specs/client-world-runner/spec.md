## MODIFIED Requirements

### Requirement: 本地最小切片
客户端世界推进器 SHALL 保留本地固定 tick、命令缓冲和 dirty flush 边界；在服务端权威移动接入后，玩家移动输入 SHALL 能切换为网络请求驱动，且只有服务端成功响应才应用到本地 world。

#### Scenario: 无服务端响应不更新玩家坐标
- **WHEN** 玩家输入移动方向
- **AND** 客户端处于服务端权威移动模式
- **AND** 尚未收到 `G2C_MoveResponse`
- **THEN** 客户端不直接通过本地移动规则更新玩家坐标

#### Scenario: 成功响应应用最终坐标
- **WHEN** 客户端收到 `G2C_MoveResponse`
- **AND** `Success` 为 true
- **THEN** 客户端将玩家坐标更新为响应中的 `FinalX` 和 `FinalY`
- **AND** dirty flush 后可视化能反映该最终坐标

#### Scenario: 失败响应保持原坐标
- **WHEN** 客户端收到 `G2C_MoveResponse`
- **AND** `Success` 为 false
- **THEN** 客户端保持玩家移动前坐标
- **AND** 客户端记录 `MoveErrorCode` 和 `Reason` 供验证

#### Scenario: 本地 runner 形状保留
- **WHEN** 客户端接入服务端权威移动
- **THEN** `ClientWorldRunner` 仍负责固定 tick 推进和系统调度
- **AND** 移动结果应用仍进入客户端 world 与 dirty flush 链路

