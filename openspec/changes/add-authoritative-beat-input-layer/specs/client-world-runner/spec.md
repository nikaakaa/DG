## ADDED Requirements
### Requirement: 客户端普通输入提交边界
Unity 客户端 SHALL 将普通玩家操作提交为方向或动作意图，并把服务端 snapshot/delta 作为 `ClientMapWorld` 权威镜像的唯一更新来源。客户端普通输入层 MAY 播放非权威反馈，但 MUST NOT 本地裁决最终坐标。

#### Scenario: 普通输入提交方向意图
- **WHEN** 玩家在服务端权威模式下按下移动方向
- **THEN** 客户端提交 entityId、clientInputId、beatTick、direction 和 clientTick
- **AND** 普通输入不以客户端目标格作为权威裁决语义

#### Scenario: 输入反馈不修改权威镜像
- **WHEN** 客户端收到本地玩家按键
- **THEN** 客户端可以记录待确认输入或播放方向反馈
- **AND** 不直接修改 `ClientMapWorld.CoreWorld` 中该 entity 的权威坐标

#### Scenario: WorldDelta 更新镜像
- **WHEN** 客户端收到服务端 WorldDelta
- **THEN** 客户端用 WorldDelta 更新 `ClientMapWorld`
- **AND** 对应待确认输入只能被确认、清理或表现修正

#### Scenario: 输入失败不 fallback 本地规则
- **WHEN** 普通玩家输入被服务端返回 Replaced、Expired、Rejected 或失败结果
- **THEN** 客户端清理对应待确认输入和非权威反馈
- **AND** 服务端权威模式下不执行本地 movement rule 来补偿失败

### Requirement: 客户端输入层验证
Unity 客户端 SHALL 通过 EditMode 测试证明普通输入提交、待确认输入状态和权威镜像边界被分离。手动端到端验证 MUST 覆盖双客户端最终状态收敛。

#### Scenario: EditMode 覆盖待确认输入
- **WHEN** 客户端提交普通玩家输入
- **THEN** 测试能观察到该输入按 clientInputId 进入待确认记录
- **AND** `ClientMapWorld.CoreWorld` 坐标不因提交本身改变

#### Scenario: EditMode 覆盖失败清理
- **WHEN** 一个待确认输入被标记为 Replaced、Expired 或 Rejected
- **THEN** 客户端清理该输入记录
- **AND** 不产生本地权威坐标变更

#### Scenario: 手动双客户端收敛
- **WHEN** 用户启动服务端和两个 Unity 客户端
- **AND** 客户端 A 在一个 beat 窗口内快速按多个方向
- **THEN** 服务端只结算 A 的最终方向输入
- **AND** 客户端 A 和 B 最终通过同一 WorldDelta 收敛到一致权威状态
