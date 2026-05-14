# 配表治理与结构演进目标

## 文档目的

这份文档只回答一个问题：

```text
DG 的 Luban 配表后续应该如何从当前平表阶段，演进到能支撑更大项目的配置治理体系。
```

它不是已经批准的 OpenSpec 需求，也不是立即实现清单。
它用于统一后续 OpenSpec 变更前的目标、边界、约束和验证口径。

## 当前判断

当前配表工作流已经具备正确的基础方向：

```text
Excel / JSON 源数据
  -> Luban 生成 C# 表代码
  -> Luban 生成 JSON 数据
  -> Unity StreamingAssets
  -> LubanGameConfigProvider
  -> IGameConfigProvider
  -> GameWorld / Rules
```

这个方向是可以继续扩展的。

但当前表结构仍然偏简单，主要是：

```text
平铺字段
少量 list#sep=, 字段
少量外挂参数表
少量运行时手写校验
少量 Unity TestFramework 覆盖
```

例如：

```text
EntityArchetype
  config_id
  archetype_id
  entity_target
  components
  tags
  default_auto_move_interval_ticks

ActionSpec
  spec_id
  primitive
  source
  priority
  source_tag
  ability_tag
  required_tags
  blocked_tags
  target_rule
  blocked_policy
  handoff_policy
  handoff_spec_id
  subject_policy
  plan_rule
  commit_rules
  default_cost_ticks
```

这说明当前不是复杂嵌套配置体系，而是把关键规则参数先从代码里抽到表里。
这在项目早期是合理的，但不能直接等同于大型项目级配表治理。

## 核心目标

长期目标不是做一个任意脚本系统。

长期目标是：

```text
配置表达稳定数据和有限策略
代码表达规则执行和世界修改
测试阻止错误配置进入运行时
provider 隔离 Luban 生成类型和 GameCore 规则层
```

更具体地说：

```text
策划可以通过配置组合实体和调整有限策略
程序可以通过强校验发现脏数据
规则层不根据字符串名字猜语义
运行时不直接依赖 Luban 生成表类型
配置结构可以逐步复杂，但复杂度必须服务于真实字段边界
```

## 第一阶段：配置治理优先

第一阶段不急着大规模拆表。

应该先补一个统一配置校验层，目标是让错误数据在测试或加载阶段失败，而不是进入运行时才表现为奇怪行为。

建议新增 Unity TestFramework EditMode 覆盖：

```text
GameConfigValidationTests
```

最小校验项：

```text
WorldSpawn.config_id 必须存在于 EntityArchetype
PlayerSpawnRule.player_config_id 必须存在于 EntityArchetype
PushOnEnterConfig.output_spec_id 必须存在于 ActionSpec
ActionSpec.handoff_spec_id 非空时必须存在于 ActionSpec
ActionSpec.spec_id 不允许为空
ActionSpec.spec_id 不允许重复
EntityArchetype.components 不允许重复
需要参数表的 ComponentKind 必须存在对应配置
不需要参数表的 ComponentKind 不应该误填外挂配置
default_cost_ticks 必须大于 0
output_cost_ticks 必须大于 0
max_chain_depth 必须在有效范围内
PlayerSpawnRule.max_attempts 必须大于 0
PortConnectorConfig.ports 只能包含合法方向位
生成 JSON 必须和 Unity StreamingAssets 保持一致
```

如果后续继续保留 Fallback provider，也要校验：

```text
Fallback provider 与正式 Luban provider 的核心行为语义不漂移
正式服务端路径不得静默回退到 Fallback provider
测试路径使用 Fallback provider 时必须显式可见
```

这一阶段的目标是先建立配置护栏。

## 第二阶段：把天然成组的字段拆成 bean

当某张表开始出现大量相互关联字段时，才拆 bean。

拆分原则：

```text
字段天然成组
字段有共同生命周期
字段有共同校验规则
字段在多个表中复用
字段未来可能出现重复子项
```

不应该因为想显得高级而拆。

ActionSpec 可以逐步演进为：

```text
ActionSpec
  spec_id
  primitive
  source
  target_config
  blocked_config
  handoff_config
  conflict_config
  subject_config
  execution_config
```

候选 bean：

```text
ActionTargetConfig
ActionBlockedConfig
ActionHandoffConfig
ActionConflictConfig
ActionSubjectConfig
ActionExecutionConfig
```

第一批最适合拆的是 handoff 和 blocked。

原因是它们已经有多字段绑定关系：

```text
blocked_policy
handoff_policy
handoff_spec_id
handoff_subject_policy
max_chain_depth
blocked_tags
```

这些字段单独平铺时，容易出现组合非法但类型合法的问题。

## 第三阶段：Component 配置从 kind 列表演进为 spec 列表

当前实体组件组合是：

```text
components = Position,Direction,Collider,PushOnEnter
```

这适合无参数 component。

当 component 逐渐有参数时，应该演进为：

```text
components = list<ComponentSpec>
```

候选结构：

```text
ComponentSpec
  kind
  params
```

或者更长期：

```text
ComponentSpec
  PositionSpec
  DirectionSpec
  ColliderSpec
  AutoMoveSpec
  PushOnEnterSpec
  PortConnectorSpec
```

这样可以把当前的外挂表逐步收束：

```text
PushOnEnterConfig
PortConnectorConfig
AutoMoveConfig
ColliderConfig
```

但这个阶段不能过早做。

触发条件应该是：

```text
至少三个 ComponentKind 需要参数表
同一个 entity 需要多个同类参数项
外挂表数量开始让 EntityArchetype 难以理解
组件参数校验开始分散在多个 provider 方法里
```

## 第四阶段：有限策略表，而不是脚本表

后续可以把一些稳定分支抽成策略表。

例如：

```text
BlockedResultPolicy
BlockedResultBranch
ActionCondition
BlockedResult
```

但策略表必须是有限枚举组合，不是任意表达式。

允许：

```text
HasTag
MissingTag
HasComponent
MissingComponent
CanBePushed
BodyKindIs
DeriveAction
Reject
BounceSource
Noop
```

不允许第一阶段引入：

```text
脚本 VM
任意表达式语言
字符串拼接执行逻辑
运行时反射调用规则函数
策划表直接修改 GameWorld
```

## 关键约束

### 配表只表达数据和有限策略

表可以表达：

```text
这个 entity 有哪些 component
这个 action 的 subject policy 是什么
遇阻时走哪个有限策略
handoff 到哪个 ActionSpecId
默认 cost 是多少
```

表不应该表达：

```text
如何扫描世界
如何提交移动
如何修改 GameWorld
如何广播 delta
如何执行任意流程脚本
```

### 规则层不能靠字符串名字猜语义

`mechanism_push`、`player_push`、`connected_body_move` 只能是 id。

规则层需要的语义必须来自：

```text
ActionSpec.subject_policy
ActionSpec.handoff_policy
ActionSpec.blocked_policy
ActionSpec.plan_rule
ActionSpec.commit_rules
```

不允许出现：

```text
if spec_id == "mechanism_push" then ...
if spec_id contains "connected_body" then ...
```

### 嵌套只服务于真实边界

可以拆：

```text
handoff_config
blocked_result_config
component_spec
condition list
result branch list
```

不建议拆：

```text
只有一个字段的包装 bean
没有复用的临时 bean
只是为了让表看起来复杂的嵌套
```

### 所有跨表引用必须可测试

跨表引用包括：

```text
config_id
spec_id
policy_id
style_id
component parameter row
world_id
entity_id
```

不能依赖人工打开 Excel 检查。

必须至少有 Unity TestFramework EditMode 覆盖。

### Runtime 继续走 provider 边界

必须保持：

```text
GameWorld / Rules
  -> IGameConfigProvider / ActionSpecRegistry
  -> mapped domain config
  -> Luban generated tables
```

不允许：

```text
GameWorld 直接引用 cfg.gamecore.*
RulePlanner 直接读取 Luban table row
ActionArbiter 直接依赖 Excel 字段名
```

### 配置复杂度必须有停止条件

出现以下情况时应该暂停继续抽象：

```text
新增一层嵌套后没有减少任何重复字段
新增配置后测试没有覆盖非法数据
配置比代码分支更难理解
策划需要记住隐含组合规则才能填表
同一语义同时存在于 tag、component、ActionSpec 三处
```

## 推荐演进顺序

```text
1. 补 GameConfigValidationTests
2. 补 Luban schema 中能直接表达的 ref / range / size 校验
3. 校验 ActionSpec handoff、PushOnEnter、WorldSpawn、PlayerSpawnRule 的跨表引用
4. 梳理哪些 ComponentKind 必须有参数配置
5. 只挑 handoff 或 blocked 这类真实膨胀字段拆 bean
6. 当参数 component 足够多时，再演进 ComponentSpec
7. 当遇阻分支继续膨胀时，再引入有限策略表
```

这个顺序的核心是：

```text
先让错误数据进不来
再让复杂数据结构变清楚
最后才让策略配置更强
```

## 不建议的演进顺序

不建议直接做：

```text
全量重构所有表
一次性把所有平铺字段改成嵌套 bean
立刻引入多态 ComponentSpec
立刻做通用规则引擎
立刻做脚本式配置
为了嵌套而嵌套
```

原因是当前规则系统仍在演进。
过早复杂化 schema 会让调试成本上升，也会让后续 OpenSpec 变更更难落地。

## 测试要求

所有配置结构演进必须有测试。

自动测试只使用 Unity 自带 TestFramework。

最低测试层次：

```text
EditMode: Luban 生成数据能加载
EditMode: 生成 JSON 与 StreamingAssets 一致
EditMode: 跨表引用合法
EditMode: 非法配置能被测试或加载阶段拒绝
EditMode: provider 映射后 domain config 与预期一致
EditMode: 关键 ActionSpec 行为保持迁移前一致
```

端到端测试由用户手动验证：

```text
启动服务端
启动 Unity Play Mode
连接单客户端或双客户端
摆放 player、pushable、conveyor、wind field、port connected body
触发 player_move、mechanism_push、auto_move
确认两个客户端最终 WorldDelta 一致
确认客户端不本地裁决规则语义
```

## OpenSpec 触发条件

以下工作需要走 OpenSpec：

```text
新增配置能力
改变现有表结构
改变 provider 映射语义
引入 ComponentSpec
引入 BlockedResultPolicy
改变 ActionSpec 字段含义
改变运行时规则解释方式
```

以下工作可以作为普通修复或测试增强：

```text
补已有表的跨表校验测试
补生成 JSON 与 StreamingAssets 一致性测试
补缺失非法数据测试
修正文档
修正明显错误的配置数据
```

## 当前自检

这份文档没有宣称当前配表体系已经能完整支撑大型项目。

当前真实状态是：

```text
Luban 工作流已经接入
provider 边界是正确方向
核心规则参数已经开始配表化
表结构仍然以平铺字段为主
复杂嵌套 bean 当前基本没有展开
Luban 自带 ref / range / size 校验还没有系统使用
跨表校验主要依赖部分手写运行时检查和现有测试
```

因此下一步目标应该叫：

```text
配置治理阶段
```

而不是：

```text
复杂嵌套表重构阶段
```

先建立护栏，再演进结构。
