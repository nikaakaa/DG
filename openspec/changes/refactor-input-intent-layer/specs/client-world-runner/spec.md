## MODIFIED Requirements

### Requirement: 客户端普通输入提交边界
Unity 客户端 SHALL 使用 Unity Input System 作为正式普通玩家输入入口，将普通玩家操作转换为声明型 `InputIntent` 并提交给服务端。客户端 `InputIntent` SHALL 表达输入事实，包括 actor、input kind、direction、target hint、client input id、client tick、beat tick 和 rhythm judge 占位；客户端 MAY 基于 pending intent 播放非权威反馈，但 MUST NOT 本地裁决最终坐标。服务端 snapshot/delta 仍 SHALL 是 `ClientMapWorld` 权威镜像的唯一更新来源。

#### Scenario: Unity Input System 生成移动意图
- **WHEN** 玩家在服务端权威模式下通过 Unity Input System 触发移动方向
- **THEN** 客户端生成 `InputKind = Move` 的 `InputIntent`
- **AND** 该 intent 包含 actor entity id、clientInputId、clientTick、direction 和 beatTick 占位
- **AND** 普通输入不以客户端目标格作为权威裁决语义

#### Scenario: Rhythm judge 第一阶段为空
- **WHEN** 客户端生成普通移动 `InputIntent`
- **THEN** intent MAY 携带 `RhythmJudge = None`
- **AND** 第一阶段不要求真实 `Perfect / Good / Late / Miss` 判定

#### Scenario: TargetHint 只是提示
- **WHEN** 客户端为点击、瞄准、交互或方向输入生成 `TargetHint`
- **THEN** `TargetHint` 只表达客户端选择提示
- **AND** 客户端不得把 `TargetHint` 当作服务端已确认目标

#### Scenario: 输入反馈不修改权威镜像
- **WHEN** 客户端收到本地玩家输入并生成 pending intent
- **THEN** 客户端可以记录待确认输入或播放方向、目标、高亮、起手等非权威反馈
- **AND** 不直接修改 `ClientMapWorld.CoreWorld` 中该 entity 的权威坐标

#### Scenario: WorldDelta 更新镜像
- **WHEN** 客户端收到服务端 WorldDelta
- **THEN** 客户端用 WorldDelta 更新 `ClientMapWorld`
- **AND** 对应待确认 intent 只能被确认、清理或表现修正

#### Scenario: 输入失败不 fallback 本地规则
- **WHEN** 普通玩家 intent 被服务端返回 Replaced、Expired、Rejected、Unauthorized 或失败结果
- **THEN** 客户端清理对应待确认 intent 和非权威反馈
- **AND** 服务端权威模式下不执行本地 movement rule 来补偿失败

### Requirement: 客户端输入层验证
Unity 客户端 SHALL 通过 Unity TestFramework EditMode 测试证明 Unity Input System 入口、`InputIntent` 生成、待确认输入状态和权威镜像边界被分离。手动端到端验证 MUST 覆盖双客户端最终状态收敛。

#### Scenario: EditMode 覆盖 InputIntent 生成
- **WHEN** 测试驱动 Unity Input System 方向输入
- **THEN** 客户端输入层生成 `InputKind = Move` 的 `InputIntent`
- **AND** intent direction 与输入方向一致

#### Scenario: EditMode 覆盖待确认输入
- **WHEN** 客户端提交普通玩家 `InputIntent`
- **THEN** 测试能观察到该 intent 按 clientInputId 进入待确认记录
- **AND** `ClientMapWorld.CoreWorld` 坐标不因提交本身改变

#### Scenario: EditMode 覆盖失败清理
- **WHEN** 一个待确认 intent 被标记为 Replaced、Expired、Rejected 或 Unauthorized
- **THEN** 客户端清理该 intent 记录
- **AND** 不产生本地权威坐标变更

#### Scenario: 手动双客户端收敛
- **WHEN** 用户启动服务端和两个 Unity 客户端
- **AND** 客户端 A 在一个 tick 或 beat 窗口内快速按多个方向
- **THEN** 服务端只结算 A 的最终方向 intent
- **AND** 客户端 A 和 B 最终通过同一 WorldDelta 或可见 delta 收敛到一致权威状态

