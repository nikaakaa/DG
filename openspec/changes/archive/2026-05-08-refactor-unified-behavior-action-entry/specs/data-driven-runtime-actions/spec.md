## MODIFIED Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置和运行时 behavior action unit 分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划、提交和 result branch 策略；behavior action unit SHALL 只携带一次运行时输入的 `SpecId`、发起者、目标、方向、tick、client tick、source context、owner/parent/derived id 和 runtime params。

#### Scenario: 运行时 action unit 引用配置
- **WHEN** 玩家输入、自动 tick、机关触发、运行时组件结果、调试入口或 push handoff 创建一个运行时行为
- **THEN** 运行时行为携带 `SpecId`
- **AND** 运行时行为不复制 `ActionSpec` 的 claim、conflict、interrupt、merge、plan、commit 或 result branch 静态策略

#### Scenario: 修改普通行为策略
- **WHEN** 一个普通行为需要调整 blocked tags、priority、handoff policy 或 conflict policy
- **THEN** 系统通过修改该行为的配置表达策略变化
- **AND** 不需要修改核心 execution system 的业务分支

#### Scenario: action unit 追踪来源关系
- **WHEN** 一个 source action unit 派生 handoff target action unit
- **THEN** target unit 记录 source action id、target entity、direction 和 handoff reason
- **AND** source unit 不通过等待 target unit 完成来表达自身 success

### Requirement: 行为原语和行为来源分离
系统 SHALL 用行为原语表达执行类型，用 source context 表达行为来源。`Move`、`Spawn`、`Remove`、`SetComponentResult`、`ApplyRuntimeEffect` 和 `HandoffMove` SHALL be primitives or primitive candidates; `Player`、`Auto`、`Mechanism`、`RuntimeResult`、`Debug` 和 `Handoff` SHALL be source / policy context, not separate execution branches for the same primitive.

#### Scenario: 现有移动来源归一化
- **WHEN** 迁移现有玩家移动、自动移动、机关推动、调试移动和 push handoff
- **THEN** 它们都映射为 `Move` primitive or a move-compatible handoff primitive
- **AND** 它们通过 source context、priority、condition、claim、plan、commit 和 result branch policy 保留差异

#### Scenario: 新增普通移动行为
- **WHEN** 新增风场推动、陷阱拉动或冰面滑行这类普通移动行为
- **THEN** 行为配置复用 existing primitive and policy data
- **AND** 新增行为不需要新增 `WorldActionKind` 或 `BehaviorIntentKind` 业务分类 unless it introduces a new primitive

#### Scenario: 运行时结果进入同一层级
- **WHEN** runtime component result needs to cause movement, spawn, remove, or state mutation
- **THEN** it creates a behavior action unit instead of directly mutating authoritative world state
- **AND** the rule pipeline applies the result through the same arbitration and commit boundaries

### Requirement: 数据驱动仲裁 claim
系统 SHALL 在仲裁层从 behavior action unit 和 `ActionSpec` 生成 `ActionClaim`。仲裁 MUST use claims to decide current unit conflict, target occupancy, same-claim merge, exclusive resources, and handoff continuation instead of hardcoding per behavior-name conflict branches.

#### Scenario: 目标格独占
- **WHEN** 两个 accepted candidates claim 同一个 exclusive target cell
- **THEN** 仲裁层根据 conflict group、priority 和 interrupt policy 决定接受、拒绝或打断
- **AND** 执行层不通过具体行为名重新判断该冲突

#### Scenario: 同 entity abstraction 合并
- **WHEN** 同一个 action unit entity abstraction 在同一 tick 收到多个相同 movement claim
- **AND** 对应配置允许 same-claim merge
- **THEN** 仲裁层接受一个 unit
- **AND** 其他 unit 以 merged reason 结束

#### Scenario: 同 entity abstraction 冲突
- **WHEN** 同一个 action unit entity abstraction 在同一 tick 收到多个不同 movement claim
- **AND** 对应配置不允许同时成立
- **THEN** 仲裁层用数据化 conflict policy 拒绝或打断冲突请求

#### Scenario: 普通 action unit 不 claim 一串目标
- **WHEN** A 推 B 并且 B 可能继续推 C
- **THEN** A 的 claim 只覆盖 A 自己或当前 entity abstraction 的结果
- **AND** B 和 C 的 movement claim 必须来自各自独立 action unit

### Requirement: 执行层只消费统一结果
系统 SHALL keep execution focused on behavior action units, accepted unit output, plans, commit proposals, commit results, dirty state, and explicit action results. Execution MUST NOT branch on ordinary business behavior names such as player move, auto move, mechanism push, wind push, trap pull, ice slide, or push handoff.

#### Scenario: 执行统一 Move
- **WHEN** 仲裁层接受一个 `Move` behavior action unit
- **THEN** planner 将其转换为统一 move plan for the current unit
- **AND** commit layer 应用 that unit's move plan
- **AND** execution layer 不关心该 unit 来自玩家、自动 tick、机关、handoff 或调试入口

#### Scenario: Handoff 是统一结果分支
- **WHEN** 仲裁层把一个 source unit 解析为 handoff
- **THEN** execution layer records the source unit handoff result
- **AND** execution layer queues the target action unit through the same intake boundary
- **AND** execution layer does not wait for the target unit to complete before writing the source result

#### Scenario: 调试行为使用配置
- **WHEN** 调试移动、调试生成或调试删除通过权限检查
- **THEN** 它们也通过对应 ActionSpec 进入统一行为管线
- **AND** 调试来源差异通过 Debug source、priority、plan rule、commit rule 和 result branch policy 表达

### Requirement: 兼容迁移现有行为
系统 SHALL migrate existing `PlayerMove`、`AutoMove`、`MechanismPush`、`DebugMove`、`DebugSpawn`、`DebugRemove` and push handoff behavior into behavior action units. During migration, compatibility adapters MAY exist, but the target architecture MUST make source-specific behavior data-driven and MUST remove ordinary parent retry as a success path.

#### Scenario: 玩家移动保持明确结果
- **WHEN** 玩家请求合法一步移动或非法远距离移动
- **THEN** 迁移后的行为结果、最终坐标、错误码、reason 和 dirty/delta behavior match the intended covered behavior
- **AND** pushable blocker cases return handoff instead of source movement success after child retry

#### Scenario: 自动移动保持一致
- **WHEN** 拥有 final `AutoMoveComponent` 和 `DirectionComponent` 的 entity 到达移动 tick
- **THEN** 迁移后的自动移动、阻挡、反弹方向和 auto move tick update match the intended covered behavior
- **AND** auto source differences are represented by source context and spec policy

#### Scenario: 机关推动保持一致
- **WHEN** 一个 entity 位于 `PushOnEnterComponent` 地格中
- **THEN** 迁移后的 mechanism source move / push behavior enters behavior action unit intake
- **AND** blocked target handling uses reject, handoff, or configured result branch instead of direct coordinate mutation

#### Scenario: 删除旧 parent retry 语义
- **WHEN** old tests or code expect `RetriesParentAfterDerivedSuccess` or equivalent parent retry success
- **THEN** they are rewritten or removed
- **AND** new tests assert source handoff completion and independent target action unit handling

### Requirement: Unity TestFramework 和手动端到端验证
系统 SHALL include Unity TestFramework EditMode tests for behavior action unit schema, arbitration policies, handoff branches, existing behavior compatibility, and ordinary behavior extension. Manual Play Mode verification SHALL confirm server-authoritative end-to-end behavior and two-client WorldDelta sync. Unity Player build MUST NOT be required.

#### Scenario: EditMode 覆盖 action unit
- **WHEN** Unity TestFramework EditMode tests run
- **THEN** they verify action spec lookup, action unit creation, handoff target creation, claim arbitration, same-claim merge, conflict rejection, priority interruption, blocked final component conditions, and no parent retry

#### Scenario: Server verification covers handoff
- **WHEN** server authoritative verification runs
- **THEN** it covers A handoff to B, B independent movement, non-pushable failure, nested handoff, and single-unit commit expectations
- **AND** it does not require Unity Player build

#### Scenario: 手动端到端验证
- **WHEN** 用户在 Play Mode 中运行服务端权威路径和两个客户端
- **AND** 玩家移动、自动移动、机关推动或 handoff 产生服务端状态变化
- **THEN** observer 客户端收到服务端 WorldDelta 后显示一致结果
- **AND** 客户端不本地决定冲突、打断、handoff 或最终权威坐标
