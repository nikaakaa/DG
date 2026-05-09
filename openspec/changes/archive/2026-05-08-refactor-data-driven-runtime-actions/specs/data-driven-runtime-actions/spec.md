## ADDED Requirements
### Requirement: 配置层和运行时行为分离
系统 SHALL 将行为静态配置和运行时行为请求分离。`ActionSpec` SHALL 描述行为原语、来源默认值、条件、claim、冲突、打断、合并、计划和提交策略；`ActionRequest` SHALL 只携带一次运行时请求的 `SpecId`、发起者、目标、方向、tick、client tick、source state 和 runtime params。

#### Scenario: 运行时请求引用配置
- **WHEN** 玩家输入、自动 tick、机关触发或调试入口创建一个运行时行为
- **THEN** 运行时行为携带 `SpecId`
- **AND** 运行时行为不复制 `ActionSpec` 的 claim、conflict、interrupt、merge、plan 或 commit 静态策略

#### Scenario: 修改普通行为策略
- **WHEN** 一个普通行为需要调整 blocked tags、priority 或 conflict policy
- **THEN** 系统通过修改该行为的配置表达策略变化
- **AND** 不需要修改核心 execution system 的业务分支

### Requirement: 行为原语和行为来源分离
系统 SHALL 用行为原语表达执行类型，用 source context 表达行为来源。`Move`、`Spawn`、`Remove`、`SetComponentResult` 和 `ApplyRuntimeEffect` SHALL be primitives or primitive candidates; `Player`、`Auto`、`Mechanism` 和 `Debug` SHALL be source / policy context, not separate execution branches for the same primitive.

#### Scenario: 现有移动来源归一化
- **WHEN** 迁移现有玩家移动、自动移动、机关推动和调试移动
- **THEN** 它们都映射为 `Move` primitive
- **AND** 它们通过 source context、priority、condition、claim、plan 和 commit policy 保留差异

#### Scenario: 新增普通移动行为
- **WHEN** 新增风场推动、陷阱拉动或冰面滑行这类普通移动行为
- **THEN** 行为配置复用 `Move` primitive
- **AND** 新增行为不需要新增 `WorldActionKind` 或 `BehaviorIntentKind` 业务分类

### Requirement: 数据驱动仲裁 claim
系统 SHALL 在仲裁层从 `ActionRequest` 和 `ActionSpec` 生成 `ActionClaim`。仲裁 MUST use claims to decide body conflict, target occupancy, same-claim merge, exclusive resources, and pending continuation instead of hardcoding per behavior-name conflict branches.

#### Scenario: 目标格独占
- **WHEN** 两个 accepted candidates claim 同一个 exclusive target cell
- **THEN** 仲裁层根据 conflict group、priority 和 interrupt policy 决定接受、拒绝或打断
- **AND** 执行层不通过具体行为名重新判断该冲突

#### Scenario: 同 body 合并
- **WHEN** 同一个 body 在同一 tick 收到多个相同 movement claim
- **AND** 对应配置允许 same-claim merge
- **THEN** 仲裁层接受一个 request
- **AND** 其他 request 以 merged reason 结束

#### Scenario: 同 body 冲突
- **WHEN** 同一个 body 在同一 tick 收到多个不同 movement claim
- **AND** 对应配置不允许同时成立
- **THEN** 仲裁层用数据化 conflict policy 拒绝或打断冲突请求

### Requirement: 数据驱动冲突和打断策略
系统 SHALL 通过配置化 `ConflictPolicy`、`InterruptPolicy`、`MergePolicy` 和 priority 决定冲突、打断和合并。新增普通行为 MUST NOT require editing core arbiter code unless it introduces a new primitive or a new reusable policy type.

#### Scenario: 高优先级打断低优先级
- **WHEN** 一个 Player source request 和一个 Auto source request 对同一 body 或 claim group 产生冲突
- **AND** 配置声明 higher-priority interrupts lower-priority
- **THEN** 仲裁层接受高优先级 request
- **AND** 低优先级 request 以 interrupted reason 结束

#### Scenario: 新增策略类型
- **WHEN** 一个新行为需要当前 registry 不支持的全新冲突策略
- **THEN** 系统 MAY 扩展 reusable policy registry
- **AND** 该扩展 MUST include tests proving existing policies remain unchanged

### Requirement: 仲裁读取 final Component 结果
系统 SHALL 在行为仲裁和计划中读取 `GameWorld` final Component result 作为规则输入。仲裁层 MUST NOT query `RuntimeEffectStore`、`AbilityKind` or `EffectKind` to decide whether an action is accepted, rejected, interrupted, merged, or blocked.

#### Scenario: 运行时效果改变移动权限
- **WHEN** 一个运行时效果已经被解析为 final movement permission component result
- **AND** 行为仲裁处理一个移动 request
- **THEN** 仲裁层读取 final component result 判断是否可移动
- **AND** 仲裁层不读取 runtime effect source 数据

#### Scenario: 运行时效果改变可推动能力
- **WHEN** 一个运行时效果已经被解析为 final pushable component result
- **AND** push 或 mechanism movement request 需要判断目标是否可推动
- **THEN** 规则层读取 final component result
- **AND** 规则层不通过 ability 或 effect 名称判断

### Requirement: 执行层只消费统一结果
系统 SHALL keep execution focused on accepted actions, plans, commit proposals, commit results, dirty state, and action results. Execution MUST NOT branch on ordinary business behavior names such as player move, auto move, mechanism push, wind push, trap pull, or ice slide.

#### Scenario: 执行统一 Move
- **WHEN** 仲裁层接受一个 `Move` request
- **THEN** planner 将其转换为统一 move plan
- **AND** commit layer 应用 move plan
- **AND** execution layer 不关心该 request 来自玩家、自动 tick、机关或调试入口

#### Scenario: 调试行为使用配置
- **WHEN** 调试移动、调试生成或调试删除通过权限检查
- **THEN** 它们也通过对应 ActionSpec 进入统一行为管线
- **AND** 调试来源差异通过 Debug source、priority、plan rule 和 commit rule 表达

### Requirement: 普通新增行为不修改核心代码
系统 SHALL allow a new ordinary behavior built from existing primitives and policies to be added by data configuration, registry data, and tests. It MUST NOT require new branches in core execution, arbitration, planning, or commit orchestration.

#### Scenario: 新增风场推动
- **WHEN** 新增一个 `wind_push` 行为，使用 existing `Move` primitive、mechanism-like source、exclusive target cell claim 和 blocked tag policy
- **THEN** 开发者新增或修改行为配置
- **AND** 添加 Unity TestFramework EditMode 覆盖
- **AND** 不修改核心 execution / arbitration orchestration code

#### Scenario: 新增底层原语
- **WHEN** 新需求无法由已有 primitive 和 reusable policies 表达
- **THEN** 系统 MAY add a new primitive or policy type
- **AND** 该新增 MUST be treated as core extension with explicit tests and proposal/task coverage

### Requirement: 兼容迁移现有行为
系统 SHALL migrate existing `PlayerMove`、`AutoMove`、`MechanismPush`、`DebugMove`、`DebugSpawn` and `DebugRemove` behavior without changing their externally observed outcomes. During migration, compatibility adapters MAY exist, but the target architecture MUST make source-specific behavior data-driven.

#### Scenario: 玩家移动保持一致
- **WHEN** 玩家请求合法一步移动或非法远距离移动
- **THEN** 迁移后的行为结果、最终坐标、错误码、reason 和 dirty/delta behavior match the current covered behavior

#### Scenario: 自动移动保持一致
- **WHEN** 拥有 final `AutoMoveComponent` 和 `DirectionComponent` 的 entity 到达移动 tick
- **THEN** 迁移后的自动移动、阻挡、反弹方向和 auto move tick update match the current covered behavior

#### Scenario: 机关推动保持一致
- **WHEN** 一个 entity 位于 `PushOnEnterComponent` 地格上
- **THEN** 迁移后的 mechanism source move / push behavior match the current covered behavior

### Requirement: Unity TestFramework 和手动端到端验证
系统 SHALL include Unity TestFramework EditMode tests for data-driven action schema, arbitration policies, existing behavior compatibility, and ordinary behavior extension. Manual Play Mode verification SHALL confirm server-authoritative end-to-end behavior and two-client WorldDelta sync. Unity Player build MUST NOT be required.

#### Scenario: EditMode 覆盖数据驱动仲裁
- **WHEN** Unity TestFramework EditMode tests run
- **THEN** they verify action spec lookup, request creation, claim arbitration, same-claim merge, conflict rejection, priority interruption, and blocked final component conditions

#### Scenario: 手动端到端验证
- **WHEN** 用户在 Play Mode 中运行服务端权威路径和两个客户端
- **AND** 玩家移动、自动移动或机关推动产生服务端状态变化
- **THEN** observer 客户端收到服务端 WorldDelta 后显示一致结果
- **AND** 客户端不本地决定冲突、打断或最终权威坐标
