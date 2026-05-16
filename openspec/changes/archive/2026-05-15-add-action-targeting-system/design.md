## Context

当前权威 action 管线已经向 `ActionContext -> ActionTargetData -> ActionExecutionOutput -> Claim -> Planning -> Commit` 演进。代码中现有 `ActionTargetSelector` 能从 `ActionTargetRule` 生成部分 `ActionTargetData`，包括 `Self`、方向格、目标坐标和 `FrontEntities`。这说明目标数据模型已经起步，但目标选择算法还没有成为可注册、可扩展、可由配置映射的独立系统。

目标文档要求的 Targeting / Query 层更大：它应该负责基于 action context 和 world 生成候选目标集合，支持目标过滤、命中顺序、目标实体、目标坐标、目标 body、命中 cell、距离、方向和 query id。它不应该继续被折叠进 `ActionArbiter`、`ActionTargetSelector` 或特定 strategy。

## Goals

- 把目标选择从单个 `ActionTargetRule` 枚举升级为 selector 类/扩展类注册模型。
- 让配置字段映射到具体 target selector 和参数，而不是让配置字段本身承载全部算法。
- 让 `TargetingSystem` 成为 action context 后、execution 前的固定阶段。
- 先实现底层抽象、注册、配置映射和现有基础目标选择迁移。
- 让 target filter 只读取 final component/tag/world fact。
- 让多目标结果排序确定、可测试、可复现。
- 保持 `Shared/DG.GameCore` 纯 C#。

## Non-Goals

- 不把 Targeting 变成行为树、脚本图或可执行节点图。
- 不在 Targeting 中做 blocked policy、push handoff、bounce、interrupt、merge、plan 或 commit。
- 不让客户端本地决定 action 命中结果。
- 不把 `FrontLine`、`FrontEntities`、box、circle 等范围算法塞进当前切片；它们作为后续 selector 类扩展。
- 不把 partial success 的完整玩法语义塞进本变更；这里只保证目标集合和后续 fanout 有明确输入。

## Decisions

### Decision: TargetingSystem 是只读查询阶段

`TargetingSystem` 消费：

```text
GameWorld
ActionContext
ActionSpec.TargetingPolicyId 或等价字段
TargetingSpec
TargetFilterPolicy
ITargetSelector registry
```

输出：

```text
TargetData[]
Direction
PrimaryTargetCoord
TargetingFailure
```

Targeting 只读 `GameWorld`。它不创建 `ActionClaim`，不创建 `CommitProposal`，不创建 `DeferredAction`，不修改 component/tag/dirty，不派生 action。

### Decision: Target 选择算法是独立类或扩展类

每一种目标选择算法都应该是独立 selector 类或扩展类，例如：

```text
SelfTargetSelector
DirectionCellTargetSelector
TargetCoordSelector
FrontLineTargetSelector
BoxAreaTargetSelector
CircleAreaTargetSelector
```

本变更只要求建立底层接口、注册表、配置映射和基础 selector 迁移。`FrontLineTargetSelector`、`BoxAreaTargetSelector`、`CircleAreaTargetSelector` 等具体范围算法不在当前实现切片中落地。

配置字段负责选择 selector 和提供参数，例如：

```text
targeting_selector_id
direction_source
origin_binding
range
max_targets
filter_policy_id
ordering_policy
```

规则层通过已解析的 selector id 找到具体 selector，不通过 action 名字、entity 名字或 tag 组合反推算法。

### Decision: TargetingSpec 替代继续膨胀的 ActionTargetRule

`ActionTargetRule` 可以作为迁移期兼容入口，但普通新行为应该引用 `TargetingSpec`。`TargetingSpec` 至少需要表达：

```text
SelectorId
DirectionSource
OriginBinding
Range
MaxTargets
FilterPolicyId
OrderingPolicy
FallbackWhenEmpty
```

第一阶段只要求这些字段覆盖现有基础行为：

```text
Self
DirectionCell
TargetCoordOneStep
TargetCoordAny
```

`FrontLine`、`FrontEntities`、box、circle 等范围类能力后续通过新增 selector 类扩展，不作为本次底层切片的完成条件。

### Decision: TargetFilter 读取 final fact

TargetFilter 读取 final component/tag/world fact，例如：

```text
Has Position
Has Collider
Has Pushable
Has Blocking
Has or missing WorldTag
Is not source subject
Is in queried cell
```

TargetFilter 不读取 action 名字，不读取 entity 名字，不读取 runtime effect 源数据，不读取 Unity 表现状态。

### Decision: TargetData 是候选目标，不是裁决结果

`TargetData` 可以包含：

```text
TargetEntityId
TargetCoord
TargetBodyId 或 SubjectKey
HitCell
HitOrder
Distance
Direction
QueryId
FilterResult
```

它不表示目标一定会被移动、被命中或被提交。后续 execution 才把 target data 转换成 claims 或 effect applications，arbitration/planning/commit 继续负责最终安全。

### Decision: 多目标输出必须稳定排序

Targeting 输出必须有明确排序契约。底层系统需要提供排序策略入口，例如：

```text
hit order
cell coordinate
entity id
query id
```

不得依赖字典遍历顺序、Unity 对象顺序、客户端到达顺序或日志字符串。

### Decision: 配置导入解析可读名

Luban/Excel 可以保留可读 `targeting_selector_id`、`targeting_id`、`filter_id`、tag 名称和 authoring 名称，但进入 `Shared.DG.GameCore` 后必须解析为 typed/numeric id 或 enum。权威规则层只消费解析后的 selector/spec/filter/policy。

## Migration Plan

1. Proposal 阶段固定 spec、design、tasks，并通过 OpenSpec strict validate。
2. Apply 阶段先增加 targeting selector 接口、注册表、`TargetingSpec`、`TargetFilter`、`TargetingSystem` 模型和纯单元测试，不改外部玩法行为。
3. 把现有 `ActionTargetSelector` 迁移为 `TargetingSystem` 的兼容 adapter 或基础 selector 集合。
4. 把 `ActionSpec` 增加 targeting policy 引用，Luban provider 解析到 runtime registry。
5. 用现有 Self、DirectionCell、TargetCoord 行为验证兼容。
6. 增加 selector registry mapping、filter、no-world-write、deterministic ordering 的 Unity EditMode 覆盖。
7. 再把 execution/claim fanout 消费统一 TargetData 输出。
8. 最后执行 Shared build、server verification、Unity EditMode，再交给用户做 Play Mode / 双客户端验证。

## Risks / Trade-offs

- 风险：TargetingSpec 过早变成大而空的配置系统。
  - Mitigation：第一阶段只做底层 selector 抽象和现有基础 selector 迁移，范围算法后续单独 proposal。
- 风险：selector 注册变成隐藏硬编码。
  - Mitigation：配置字段选择已解析 selector id，测试覆盖相同 selector id 对不同行为名结果等价。
- 风险：和 `refactor-authoritative-action-pipeline-foundation` 重复。
  - Mitigation：该变更聚焦 Targeting selector/TargetingSpec/TargetFilter/TargetingSystem，消费既有 ActionContext/TargetData/ExecutionOutput。
- 风险：Targeting 侵入仲裁。
  - Mitigation：spec 明确 Targeting 不生成 claim、不处理 blocked、不写世界。
- 风险：配置名继续泄漏到规则层。
  - Mitigation：导入边界必须解析为 typed/numeric runtime id，测试覆盖普通行为改名不改变结果。
