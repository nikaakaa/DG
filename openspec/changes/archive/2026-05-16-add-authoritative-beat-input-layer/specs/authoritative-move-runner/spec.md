## ADDED Requirements
### Requirement: 服务端权威拍点输入缓冲
服务端 SHALL 在普通玩家输入进入 action pipeline 前，按 `entityId + beatTick` 收口为该实体该拍的最终输入。普通玩家输入缓冲 MUST NOT 裁决阻挡、推动、机关、connected body 或 runtime effect 结果。

#### Scenario: 同实体同拍后输入覆盖
- **WHEN** 同一 entity 在同一 beatTick 提交多个普通玩家输入
- **THEN** 服务端只保留最后一次输入作为该拍最终输入
- **AND** 被覆盖输入完成为 Replaced
- **AND** 被覆盖输入不进入 action queue

#### Scenario: 不同实体同拍互不覆盖
- **WHEN** entity A 和 entity B 在同一 beatTick 提交普通玩家输入
- **THEN** 服务端分别保留 A 和 B 的最终输入
- **AND** A 的后续输入不会替换 B 的输入

#### Scenario: 当前拍只消费一次
- **WHEN** 服务端 drain 某个 beatTick 的普通玩家输入
- **THEN** 返回该拍每个 entity 的最终输入
- **AND** 第二次 drain 同一 beatTick 不再返回已消费输入

#### Scenario: 未来拍不提前消费
- **WHEN** 服务端当前 tick 是 N
- **AND** 缓冲中存在 beatTick 大于 N 的普通玩家输入
- **THEN** 当前 tick 不消费该输入
- **AND** 该输入不会提前进入 action queue

#### Scenario: 过期输入拒绝
- **WHEN** 普通玩家输入的 beatTick 早于服务端可接受窗口
- **THEN** 服务端完成该输入为 Expired 或 Rejected
- **AND** 该输入不进入 action queue

### Requirement: 玩家输入意图进入现有行为管线
服务端 SHALL 将已消费的普通玩家输入转换为现有 player movement action，并继续通过 `ActionSpec -> ActionRequest -> Strategy -> Arbitration -> Planning -> Commit -> WorldDelta` 管线裁决。普通玩家输入 MUST 表达方向或动作意图，服务端 MUST 使用权威当前位置推导移动目标格。

#### Scenario: 方向意图转成服务端目标格
- **WHEN** 服务端消费一个 direction 为 Right 的玩家输入
- **AND** 权威 GameWorld 中该 entity 当前坐标是 `(1,2)`
- **THEN** 服务端生成的 player movement action 目标格是 `(2,2)`
- **AND** 目标格不是直接信任客户端提交的表现坐标

#### Scenario: 行为裁决仍由 action pipeline 完成
- **WHEN** 已消费玩家输入对应目标格被阻挡或触发推动
- **THEN** 服务端通过现有 action pipeline 得到成功、失败、推动或反馈结果
- **AND** 输入缓冲层不直接修改 GameWorld 坐标

#### Scenario: WorldDelta 仍是最终同步结果
- **WHEN** 玩家输入经 action pipeline 产生坐标、组件、tag、生成或删除变化
- **THEN** 服务端通过统一 WorldDelta 同步最终权威结果
- **AND** 输入响应不替代 WorldDelta

#### Scenario: 调试编辑不走普通输入缓冲
- **WHEN** 客户端发送 debug spawn、debug drag move、debug remove 或 debug runtime effect 请求
- **THEN** 服务端按调试编辑路径处理
- **AND** 这些请求不被 `entityId + beatTick` 普通玩家输入缓冲覆盖

### Requirement: 普通输入兼容迁移
服务端 MAY 保留旧 `C2G_MoveRequest` 作为迁移期兼容入口，但旧入口 MUST 适配到同一拍点输入缓冲。普通移动请求 MUST NOT 继续以 FIFO 多请求形式直接进入 player movement action queue。

#### Scenario: 旧移动请求进入拍点缓冲
- **WHEN** 客户端通过旧移动请求提交普通移动
- **THEN** 服务端把该请求适配为 direction/beat 输入
- **AND** 该输入参与同一实体同一拍覆盖规则

#### Scenario: 旧目标格不作为权威裁决来源
- **WHEN** 旧移动请求包含 TargetX 和 TargetY
- **THEN** 服务端可以用它们推导方向或校验相邻移动
- **AND** 最终 player movement action 的目标格来自服务端权威当前位置和方向

#### Scenario: FIFO 多移动被禁止
- **WHEN** 同一 entity 在同一 beatTick 通过旧移动请求提交多个方向
- **THEN** 服务端只生成一个 player movement action
- **AND** 最终 action 对应最后一次输入
