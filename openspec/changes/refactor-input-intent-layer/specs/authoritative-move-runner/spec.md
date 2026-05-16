## MODIFIED Requirements

### Requirement: 服务端权威拍点输入缓冲
服务端 SHALL 在普通玩家输入进入 action pipeline 前，将客户端声明型输入归一化为服务端认可的 `InputIntent`，并按 actor entity id + 服务端消费窗口 tick + input channel 收口为该 actor 该窗口的最终输入事实。客户端声明的 beat tick MAY 作为节奏参考字段保存，但 MUST NOT 直接决定普通玩家输入的服务端消费窗口。普通输入缓冲 MUST NOT 裁决阻挡、推动、机关、connected body、cooldown 或 runtime effect 结果。

#### Scenario: 同 actor 同服务端窗口后输入覆盖
- **WHEN** 同一 actor 在同一服务端消费窗口和普通移动输入通道提交多个 `Move` intent
- **THEN** 服务端只保留最后一次 intent 作为该窗口最终输入事实
- **AND** 被覆盖 intent 完成为 Replaced
- **AND** 被覆盖 intent 不进入 action queue

#### Scenario: 不同 actor 同服务端窗口互不覆盖
- **WHEN** actor A 和 actor B 在同一服务端消费窗口提交普通输入 intent
- **THEN** 服务端分别保留 A 和 B 的最终输入事实
- **AND** A 的后续 intent 不会替换 B 的 intent

#### Scenario: 当前服务端窗口只消费一次
- **WHEN** 服务端 drain 某个消费窗口的普通输入 intent
- **THEN** 返回该窗口每个 actor 的最终 intent
- **AND** 第二次 drain 同一消费窗口不再返回已消费 intent

#### Scenario: 未来服务端窗口不提前消费
- **WHEN** 服务端当前 tick 是 N
- **AND** 缓冲中存在 consumeTick 大于 N 的普通输入 intent
- **THEN** 当前 tick 不消费该 intent
- **AND** 该 intent 不会提前进入 action queue

#### Scenario: 客户端 BeatTick 不会让快速输入自动滚入多个未来 tick
- **WHEN** 同一 actor 在服务端同一收集窗口内快速提交多个 `Move` intent
- **AND** 这些请求没有明确启用 input-ahead
- **THEN** 服务端把它们归入同一个服务端消费窗口
- **AND** 只保留最后一次 intent
- **AND** 不会因为处理时 `World.ServerTick` 前进而把这些 intent 自动分配到多个未来 tick

#### Scenario: 过期输入拒绝
- **WHEN** 普通输入 intent 的服务端消费窗口早于服务端可接受窗口
- **THEN** 服务端完成该 intent 为 Expired 或 Rejected
- **AND** 该 intent 不进入 action queue

### Requirement: 输入意图授权边界
服务端 SHALL 在普通 `InputIntent` 进入输入缓冲或 action pipeline 前执行授权。授权 MUST 校验来源、session 与 actor 控制关系、input id 去重、tick/beat 窗口和限流。授权 MAY 读取 `PlayerControlComponent` 以及 RuntimeEffect / 状态产生的最终组件事实来判断当前 tick 是否可控制 actor。授权 MUST NOT 裁决碰撞、推动、机关、目标合法性、技能冷却或最终坐标。

#### Scenario: 普通玩家 actor 授权
- **WHEN** Player 来源的 intent 声明 ActorEntityId
- **THEN** 服务端校验该 session 是否在当前最终组件事实下可控制该 actor
- **AND** 只有授权通过的 intent 可以进入普通输入缓冲

#### Scenario: RuntimeEffect 可以影响控制权
- **WHEN** 某个 actor 的最终组件事实表示当前 tick 禁止、转移或限制玩家控制
- **THEN** 服务端授权按最终事实接受或拒绝 Player 来源 intent
- **AND** 授权层不需要知道具体 effect 玩法名称

#### Scenario: 授权失败不执行规则
- **WHEN** intent 因 Unauthorized、InvalidActor、InvalidSource、RateLimited、Duplicate 或 Expired 被拒绝
- **THEN** 服务端返回输入生命周期或授权失败状态
- **AND** 该 intent 不进入 action pipeline

#### Scenario: 授权通过仍可规则失败
- **WHEN** Player 来源 Move intent 授权通过
- **AND** 后续 action pipeline 发现目标格被阻挡
- **THEN** 输入授权仍视为通过
- **AND** 行动失败由 action result 表达

### Requirement: 玩家输入意图进入现有行为管线
服务端 SHALL 将已授权、已消费的 `InputIntent` 通过 `IntentAdapter` 转换为现有 player movement action，并继续通过 `ActionSpec -> ActionRequest -> Strategy -> Arbitration -> Planning -> Commit -> WorldDelta` 管线裁决。普通玩家 `Move` intent MUST 表达 direction 或等价移动意图，服务端 MUST 使用权威当前位置推导移动目标格。

#### Scenario: Move intent 转成服务端目标格
- **WHEN** 服务端消费一个 `InputKind = Move` 且 direction 为 Right 的 intent
- **AND** 权威 GameWorld 中该 actor 当前坐标是 `(1,2)`
- **THEN** 服务端生成的 player movement action 目标格是 `(2,2)`
- **AND** 目标格不是直接信任客户端提交的表现坐标或 target hint

#### Scenario: TargetHint 由权威 targeting 重新解析
- **WHEN** intent 携带 entity、cell 或 direction 类型的 `TargetHint`
- **THEN** 服务端把它作为目标提示输入
- **AND** action targeting 使用权威 GameWorld 重新解析目标

#### Scenario: 行为裁决仍由 action pipeline 完成
- **WHEN** 已消费 Move intent 对应目标格被阻挡或触发推动
- **THEN** 服务端通过现有 action pipeline 得到成功、失败、推动或反馈结果
- **AND** 输入缓冲层和 IntentAdapter 不直接修改 GameWorld 坐标

#### Scenario: WorldDelta 仍是最终同步结果
- **WHEN** 玩家 intent 经 action pipeline 产生坐标、组件、tag、生成或删除变化
- **THEN** 服务端通过统一 WorldDelta 同步最终权威结果
- **AND** 输入响应不替代 WorldDelta

#### Scenario: 调试编辑不走普通输入缓冲
- **WHEN** 客户端发送 debug spawn、debug drag move、debug remove 或 debug runtime effect 请求
- **THEN** 服务端按调试编辑路径处理
- **AND** 这些请求不被普通 Player intent 合并规则覆盖

### Requirement: 普通输入兼容迁移
服务端 MAY 保留旧 `C2G_PlayerInputRequest` 或 `C2G_MoveRequest` 作为迁移期兼容入口，但旧入口 MUST 适配到同一 `InputIntent` 缓冲。普通移动请求 MUST NOT 继续以 FIFO 多请求形式直接进入 player movement action queue。

#### Scenario: 旧移动请求进入 InputIntent 缓冲
- **WHEN** 客户端通过旧移动请求提交普通移动
- **THEN** 服务端把该请求适配为 `InputKind = Move` 的 intent
- **AND** 该 intent 参与同 actor 同 tick/beat 覆盖规则

#### Scenario: 旧目标格不作为权威裁决来源
- **WHEN** 旧移动请求包含 TargetX 和 TargetY
- **THEN** 服务端可以用它们推导 direction 或校验相邻移动
- **AND** 最终 player movement action 的目标格来自服务端权威当前位置和 direction

#### Scenario: FIFO 多移动被禁止
- **WHEN** 同一 actor 在同一服务端消费窗口通过旧移动请求提交多个方向
- **THEN** 服务端只保留最后一个适配后的 Move intent
- **AND** 最终 action 对应最后一次 intent
