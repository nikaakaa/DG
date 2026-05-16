## ADDED Requirements

### Requirement: InputIntent 与 ActionRequest 分离
系统 SHALL 将输入事实和规则请求分离。`InputIntent` SHALL 表达来源、actor、输入类型、方向、目标提示、tick/beat、client input id 和节奏判定占位等输入事实；`ActionRequest` SHALL 表达进入规则系统的行为请求。`InputIntent` MUST NOT 复制 `ActionSpec` 的静态策略，也 MUST NOT 表达碰撞、推动、机关、冷却、目标合法性或最终坐标结果。

#### Scenario: 输入事实不携带规则结果
- **WHEN** 玩家提交一个 Move intent
- **THEN** intent 只描述 actor、input kind、direction、tick/beat 和来源
- **AND** intent 不描述移动是否成功、是否撞墙、是否推动或最终坐标

#### Scenario: 规则请求由适配层创建
- **WHEN** 服务端消费一个已授权 InputIntent
- **THEN** IntentAdapter 或等价边界创建 `ActionRequest` / `WorldAction`
- **AND** 后续 action pipeline 根据 `ActionSpec` 和权威 GameWorld 裁决结果

#### Scenario: RhythmJudge 是输入事实
- **WHEN** InputIntent 携带 `RhythmJudge`
- **THEN** 该字段只表达输入窗口判定事实
- **AND** 是否增伤、破连、空拍或触发惩罚由后续显式规则决定

### Requirement: InputIntent 来源统一
系统 SHALL 允许 Player、Debug、AI、Replay、Script 等来源统一表达为 `InputIntent`，并通过来源权限边界决定哪些来源可以从客户端提交，哪些只能由服务端内部创建。普通玩家输入、调试输入、AI 输入和回放输入 MUST NOT 因来源不同而绕过后续 action pipeline 的权威裁决。

#### Scenario: Player 来源需要授权
- **WHEN** 客户端提交 Player 来源 InputIntent
- **THEN** 服务端授权层校验 session、actor 控制关系和输入生命周期
- **AND** 授权通过后才允许进入输入缓冲或 IntentAdapter

#### Scenario: Debug 来源不参与普通合并
- **WHEN** Debug 来源创建 spawn、drag、remove 或 runtime effect intent
- **THEN** 它使用显式调试权限和调试路径
- **AND** 它不被普通 Player Move intent 的同拍覆盖规则吞掉

#### Scenario: AI 和 Replay 预留同一语言
- **WHEN** 后续 AI 或 Replay 需要驱动 actor 行动
- **THEN** 它们 MAY 产出 InputIntent
- **AND** 后续仍通过 IntentAdapter 和 action pipeline 进入权威裁决

### Requirement: IntentAdapter 不硬编码普通行为策略
系统 SHALL 通过 `IntentAdapter` 或等价边界把 `InputIntent` 转成明确的 action runtime 输入。适配层 MAY 将 `Move` 映射到现有 player movement action，但普通行为策略仍 MUST 来自 `ActionSpec`、targeting、condition、claim、conflict、planning 和 commit 数据，不得在输入层或适配层通过 action 名称、entity 名称或 tag 组合硬编码玩法裁决。

#### Scenario: Move intent 映射到移动 action
- **WHEN** IntentAdapter 接收已授权 `InputKind = Move` 的 intent
- **THEN** 它创建现有 player movement action runtime 输入
- **AND** direction 作为 runtime input 进入后续 targeting 和 planning

#### Scenario: 非移动意图需要显式映射
- **WHEN** 后续新增 Attack、Interact 或 Skill intent
- **THEN** 适配层通过显式 action id 或配置映射创建 ActionRequest
- **AND** 不在输入层直接执行攻击、互动或技能结果

#### Scenario: TargetHint 不替代 TargetData
- **WHEN** InputIntent 携带 TargetHint
- **THEN** TargetHint 只作为 action request 的目标提示
- **AND** `TargetData` 仍由权威 targeting 规则解析

