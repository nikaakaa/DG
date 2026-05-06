# DG 运行时能力与效果系统规划

## 定位

DG 需要一套长期使用的运行时能力与效果系统，用来承接运行时动态能力、持续状态、临时组件、tag 贡献、效果过期和客户端镜像。

这不是 OpenSpec 输入文档，也不是某个单次功能的任务清单。它是后续一组子文档的总纲，用来固定大方向、模块边界和长期演进路线。

系统参照 UE Gameplay Ability System 的职责拆分，但落地到 DG 当前技术栈：

```text
Luban 配置定义
  -> Shared GameCore 纯 C# 运行时模型
  -> 服务端权威 tick/action/commit
  -> WorldDelta 同步
  -> Unity 客户端镜像和显示
```

## 总目标

运行时能力与效果系统要解决三类问题：

```text
Entity 当前能做什么
Entity 当前处于什么状态
这些能力和状态如何在服务端权威规则中生效并同步给客户端
```

当前静态 component-system 继续负责实体出生时的固定能力组合。运行时能力与效果系统负责实体出生之后发生的动态变化。

```text
静态 component:
  由 Luban entity archetype 配置
  在 EntityBuilder 创建实体时应用
  表达出生时能力和固定结构

运行时 ability/effect:
  由服务端 tick、规则、触发器、调试命令或后续技能触发
  在 action/commit 管线中提交
  表达运行中获得、刷新、移除、过期的能力和状态
```

## UE GAS 对标

DG 参照 UE GAS 的系统边界，不照搬 UE 的 UObject、ActorComponent、引擎复制、AttributeSet、GameplayCue 和客户端预测。

```text
UE AbilitySystemComponent
  -> DG GameWorld 上的 Ability/Effect runtime facade

UE GameplayAbility
  -> DG AbilityDefinition + WorldAction producer

UE GameplayAbilitySpec
  -> DG AbilitySpec

UE GameplayEffect
  -> DG EffectDefinition

UE GameplayEffectSpec
  -> DG RuntimeEffectSpec

UE ActiveGameplayEffect
  -> DG RuntimeEffectInstance

UE GameplayTag
  -> DG WorldTag

UE GameplayEffectContext
  -> DG EffectContext
```

对标的意义是固定职责，不是复制类层级。

## 大模块划分

### 1. 配置定义层

配置定义层回答“有哪些能力”和“有哪些效果”。

它属于 Luban，面向策划配置和长期数据维护。

```text
AbilityDefinition
EffectDefinition
AbilityKind
EffectKind
StackPolicy
PayloadSchema
ContributedTags
RuntimeComponents
```

这一层只保存定义，不保存运行时实例。

### 2. 实体能力层

实体能力层回答“某个 entity 当前拥有哪些 ability”。

它属于 Shared GameCore。

```text
AbilitySpec
AbilityActivationRequest
Ability runtime facade
```

AbilitySpec 可以来自静态 component，也可以由 runtime effect 临时授予。能力激活只产生 `WorldAction` 或 `RuntimeEffectSpec`，不直接修改 `GameWorld`。

### 3. 效果申请层

效果申请层回答“这一次想给谁施加什么效果，来源是什么”。

它属于 Shared GameCore 的运行时模型。

```text
RuntimeEffectSpec
EffectContext
EffectPayload
```

`RuntimeEffectSpec` 是还未提交的申请。`EffectContext` 记录来源 entity、目标 entity、来源 action、触发坐标和原因。

### 4. 效果实例层

效果实例层回答“世界里当前已经存在什么效果”。

它属于服务端权威 `GameWorld`。

```text
RuntimeEffectInstance
RuntimeEffectStore
RuntimeEffectId
StackPolicy runtime state
tag contribution sources
component contribution sources
```

`RuntimeEffectStore` 保存效果实例、过期 tick、叠加状态、贡献来源和 dirty 状态。它不裁决移动，不替代 `IntentArbiter`。

### 5. 效果应用层

效果应用层回答“某种 EffectKind 应用到 world 时具体贡献什么”。

它属于 Shared GameCore。

```text
EffectApplicationRegistry
EffectKind -> apply/remove
WorldTag contribution
runtime component contribution
```

这层是集中映射边界。它允许代码实现具体效果应用，但不允许散落在多个 system 里写业务 if 链。

### 6. 规则裁决层

规则裁决层回答“能力和效果如何影响服务端规则”。

它复用当前已有主路：

```text
WorldAction
  -> BehaviorIntent
  -> IntentArbiter
  -> RulePlanner
  -> ConflictResolver
  -> Commit
```

Ability/Effect 接到这条主路前后：

```text
Ability activation
  -> WorldAction / RuntimeEffectSpec
  -> EffectPlanner
  -> EffectCommitProposal
  -> CommitResolver
  -> EffectApplicationRegistry
```

移动阻断、免疫、沉默、眩晕等规则影响通过 `WorldTag` 或 runtime component 被现有仲裁层读取。

### 7. 同步镜像层

同步镜像层回答“客户端如何看到效果状态”。

它由 WorldDelta 和 ClientMapWorld 承接。

```text
WorldDelta.AddedEffects
WorldDelta.RefreshedEffects
WorldDelta.RemovedEffects
WorldDelta.ChangedTags
WorldDelta.ChangedComponents
ClientMapWorld effect mirror
Debug panel effect view
```

客户端只镜像和显示，不进行权威能力裁决或效果提交。

### 8. 工具与验证层

工具与验证层回答“如何证明这套系统是可维护的”。

它包括：

```text
Luban 导出校验
AbilityKind / EffectKind 漂移检查
Shared/server 单元验证
Unity TestFramework EditMode
手动端到端验证
Debug 面板
Sandbox 场景
```

验证重点不是单个玩法，而是证明配置、运行时、提交、同步和客户端镜像的链路成立。

## 与当前系统的关系

### 与 component-system

component-system 继续负责实体出生时的能力组合。

```text
Luban EntityArchetype
  -> ComponentKind
  -> ComponentApplicationRegistry
  -> GameWorld.SetComponent
```

运行时能力与效果系统不替代 component-system。它补的是运行中动态变化这一层。

### 与 WorldTag

`WorldTag` 对标 UE GameplayTag，是语义标记和仲裁查询语言。

`WorldTag` 表达：

```text
StateStunned
StateSilenced
ImmuneMechanismPush
BlockPlayerMove
AbilityMove
AbilityPlayerPush
```

`RuntimeEffectInstance` 表达：

```text
为什么有这个 tag
来源是谁
目标是谁
何时创建
何时过期
如何叠加
何时移除
```

效果贡献 tag，但效果不等于 tag。

### 与 Rules

Rules 仍然是权威裁决主入口。Ability/Effect 不能绕过 Rules 直接改坐标、占用、tag 或 component。

```text
效果申请、刷新、移除、过期
  -> action/commit
  -> GameWorld mutation
  -> dirty/world delta
```

### 与 ClientMapWorld

`ClientMapWorld` 只做镜像适配。

```text
收到 effect delta
  -> 更新本地 mirror
  -> 显示层读取 mirror
  -> Debug 面板显示 effect
```

客户端不本地决定 effect 是否生效。

## 长期边界

系统必须长期保持这些边界：

- 配置定义来自 Luban。
- 运行时实例存在于服务端权威 world state。
- 运行时状态不写回 Excel。
- Ability 激活不直接修改 GameWorld。
- Effect apply/refresh/remove/expire 全部走 action/commit。
- RuntimeEffectStore 不做移动裁决。
- EffectApplicationRegistry 是集中映射边界。
- WorldTag 是语义查询，不是生命周期容器。
- WorldDelta 是客户端镜像来源。
- ClientMapWorld 不写权威规则。

## 不纳入本系统的内容

这些内容不放进当前能力与效果系统总纲：

- UE UObject / ActorComponent 复刻。
- 客户端预测。
- 回滚。
- AttributeSet 数值属性系统。
- GameplayCue 表现系统。
- 技能冷却、消耗、释放条、连招。
- 复杂战斗公式。
- AOI 和网络可见性。
- 沙盒 JSON 作为正式配置源。

这些方向以后可以分别建立子文档，但不混进当前总纲。

## 领域对象关系

运行时能力与效果系统的核心对象关系如下：

```text
Entity
  owns AbilitySpec
  owns Component
  owns WorldTag through TagSetComponent
  can receive RuntimeEffectInstance

AbilityDefinition
  defines activation meaning
  defines required / blocked tags
  can emit WorldAction
  can emit RuntimeEffectSpec

AbilitySpec
  binds AbilityDefinition to one entity
  can be static from archetype
  can be granted by RuntimeEffectInstance

EffectDefinition
  defines duration
  defines stack policy
  defines contributed tags
  defines runtime components

RuntimeEffectSpec
  is a pending request
  has source / target / context / payload
  is not world state yet

RuntimeEffectInstance
  is committed world state
  has id / source / target / created tick / expire tick
  contributes tags and runtime components through EffectApplicationRegistry

RuntimeEffectStore
  owns active RuntimeEffectInstance records
  owns tag contribution sources
  owns component contribution sources
  produces dirty effect changes
```

这个关系里，`Entity` 不应该变成业务规则对象。它仍然只是 id、配置标识、tag 字符串和 component 容器的入口。规则意义放在 Ability、Effect、WorldTag、Component 和 Rules 层。

## 数据生命周期

能力与效果系统的数据从配置到客户端显示经历五个阶段：

```text
Definition
  -> Spec
  -> Request
  -> Instance
  -> Mirror
```

### Definition

Definition 是配置定义，来自 Luban。

```text
AbilityDefinition
EffectDefinition
```

Definition 是静态数据。它可以被服务端和客户端读取，但不能表达某个 entity 当前是否正在受影响。

### Spec

Spec 是 entity 当前拥有的能力记录。

```text
AbilitySpec
```

Spec 可以来自实体出生时的配置，也可以来自运行时效果授予。Spec 是运行时状态，需要跟随 entity 生命周期清理。

### Request

Request 是一次待提交申请。

```text
AbilityActivationRequest
RuntimeEffectSpec
```

Request 必须进入服务端 tick/action/commit 管线。Request 没有提交前不能改变世界。

### Instance

Instance 是已经提交的运行时效果状态。

```text
RuntimeEffectInstance
```

Instance 由服务端权威保存，随 tick 刷新、叠加、过期和移除。

### Mirror

Mirror 是客户端镜像状态。

```text
ClientMapWorld effect mirror
Debug panel state
Unity visual state
```

Mirror 来自 WorldDelta。客户端不能把 mirror 反向当成权威状态。

## 权威性边界

运行时能力与效果系统必须保持服务端权威。

```text
Client input
  -> request server action
  -> server tick validates
  -> server commit mutates GameWorld
  -> server emits WorldDelta
  -> client mirrors result
```

客户端允许做：

```text
提交输入
显示 effect
显示 tag 状态
显示倒计时
显示调试来源
播放视觉反馈
```

客户端不允许做：

```text
本地决定 ability 成功
本地决定 effect 生效
本地移除服务端 effect
本地改权威 tag
本地改权威 component
本地绕过服务端移动阻断
```

Debug 工具也要区分两种模式：

```text
本地 Shared 验证:
  用于编辑器内快速验证规则组合

服务端权威验证:
  通过 Debug RPC / action queue 修改权威 GameWorld
```

正式结论以服务端权威验证为准。

## Tick 阶段划分

能力与效果系统进入服务端 tick 后，需要稳定阶段顺序。

总方向：

```text
1. 收集外部输入 action
2. 推进已有 runtime effect 的过期检查
3. 处理 ability activation
4. 处理 runtime effect request
5. 生成 behavior intent
6. 仲裁 intent
7. 规划 plan
8. 解决冲突
9. 提交 world mutation
10. 应用 effect contribution
11. 生成 dirty / WorldDelta
```

阶段顺序的核心要求：

- 过期效果必须按 server tick 确定性处理。
- 同 tick apply/remove/refresh 必须有稳定顺序。
- effect 贡献的 tag/component 必须在后续仲裁读取前处于明确状态。
- move commit 和 effect commit 之间不能互相绕过。
- WorldDelta 必须反映最终提交后的状态。

具体阶段顺序可以在 Rules 接入子文档中细化，但总纲先固定“必须有 tick 阶段”这个方向。

## Ability 分类

Ability 是“entity 能发起什么行为”的运行时表达。

长期按触发方式分为几类：

### 主动输入能力

来自玩家输入或 AI 决策。

```text
Move
Push
Interact
UseItem
CastSkill
```

这类能力通常产生 `WorldAction` 或 `BehaviorIntent`。

### 被动触发能力

来自位置、碰撞、进入格子、离开格子、tick 条件或状态变化。

```text
ApplyEffectOnEnter
ApplyEffectOnLeave
TriggerOnCollision
TriggerOnTick
TriggerOnStateChanged
```

这类能力通常产生 `RuntimeEffectSpec` 或新的 `WorldAction`。

### 授予型能力

来自运行时效果临时授予。

```text
TemporaryMoveMode
TemporaryImmunity
TemporaryInteraction
```

这类能力需要记录 `GrantedByEffectId`，并随 effect 移除而移除。

### 调试能力

来自 Debug 工具。

```text
DebugApplyEffect
DebugRemoveEffect
DebugGrantAbility
DebugRemoveAbility
```

调试能力必须仍然能走服务端权威 action 管线，避免 Debug UI 变成第二套规则系统。

## Effect 分类

Effect 是“entity 当前受到什么运行时影响”的表达。

长期按作用方式分为几类：

### Tag 贡献型效果

只贡献 `WorldTag`，由现有仲裁层读取。

```text
BlockPlayerMove
ImmuneMechanismPush
StateStunned
StateSilenced
```

这是最基础、最适合先落地的效果类型。

### Runtime Component 贡献型效果

给目标 entity 添加运行时 component。

```text
MovementBlockedComponent
TemporaryBlockingComponent
SlowMoveComponent
```

runtime component 是纯数据，不能自己执行逻辑。

### Ability 授予型效果

临时授予目标 entity 一个 AbilitySpec。

```text
GrantTemporaryAbility
GrantTemporaryImmunityAbility
```

这类效果需要在 remove/expire 时撤回 ability。

### 规则参数型效果

影响规则计算参数。

```text
MoveCostModifier
PushPriorityModifier
ActionPriorityModifier
```

这类效果需要更谨慎，因为会影响冲突和确定性。相关细节放到 Effect 模型子文档。

### 纯表现型效果

只用于客户端显示，不影响权威规则。

```text
Highlight
Warning
Preview
```

纯表现型效果仍然可以通过 WorldDelta 同步，但不应该进入服务端裁决逻辑。

## StackPolicy 方向

StackPolicy 决定同类效果重复申请时如何处理。

总纲固定这些基础策略：

```text
RejectDuplicate
RefreshDuration
Independent
StackCount
ReplaceByPriority
```

含义：

```text
RejectDuplicate:
  已存在同类效果时拒绝新申请。

RefreshDuration:
  已存在同类效果时刷新 expire tick。

Independent:
  每次申请生成独立 RuntimeEffectInstance。

StackCount:
  同类效果合并为一个实例并增加 stack count。

ReplaceByPriority:
  新效果优先级更高时替换旧效果。
```

第一批子文档要先定义每种策略的确定性排序和测试方式，避免同 tick 多来源效果表现不稳定。

## 失败语义

能力与效果系统不能只有成功路径，还要有稳定失败语义。

Ability 激活失败原因：

```text
SourceEntityMissing
AbilityMissing
MissingRequiredTag
BlockedByTag
InvalidTarget
TargetOutOfRange
CooldownBlocked
CostBlocked
```

Effect 申请失败原因：

```text
TargetEntityMissing
EffectDefinitionMissing
RejectedByStackPolicy
TargetImmune
InvalidDuration
InvalidPayload
CommitConflict
```

当前总纲不实现 cooldown/cost，但保留失败语义位置。这样以后加冷却和消耗时，不需要重写 ability 激活结果模型。

失败结果必须能被测试和 Debug 面板观察。服务端日志、验证工具和客户端调试显示都要能区分“没有触发”“触发失败”“提交失败”“提交成功但后续被仲裁阻断”。

## 数据归属矩阵

| 数据 | 来源 | 权威位置 | 客户端是否镜像 | 是否写回 Luban |
| --- | --- | --- | --- | --- |
| AbilityDefinition | Luban Excel | 配置 provider | 可读 | 否 |
| EffectDefinition | Luban Excel | 配置 provider | 可读 | 否 |
| AbilitySpec | archetype 或 runtime effect | 服务端 GameWorld | 可镜像 | 否 |
| RuntimeEffectSpec | action / ability / trigger | tick 临时数据 | 不镜像 | 否 |
| RuntimeEffectInstance | commit | 服务端 GameWorld | 镜像 | 否 |
| WorldTag contribution | effect application | 服务端 GameWorld | 镜像 | 否 |
| runtime component contribution | effect application | 服务端 GameWorld | 镜像 | 否 |
| effect visual state | WorldDelta | Unity client | 本地显示 | 否 |

这个表是后续子文档的判断基准。任何新增字段都要明确属于哪一行。

## 配置与运行时分离

配置定义和运行时实例必须分离。

配置定义描述：

```text
有哪些 ability
有哪些 effect
默认持续多久
默认贡献哪些 tag
默认添加哪些 runtime component
默认 stack policy 是什么
payload schema 是什么
```

运行时实例描述：

```text
谁触发
谁受影响
哪一 tick 创建
哪一 tick 过期
当前 stack count
当前 payload
当前贡献来源
```

配置可以热更新或重新导出，但已经存在的运行时实例不能反向污染配置。运行时实例的存档、回放、调试另开子文档，不写入 Luban Excel。

## 与未来系统的关系

### 与技能系统

后续真正的技能系统可以建立在 Ability 层之上。

```text
Skill
  -> activates Ability
  -> emits WorldAction / RuntimeEffectSpec
  -> commits through server tick
```

技能冷却、消耗、释放条、连招不放进当前总纲，但当前 Ability 激活结果要给它们留位置。

### 与属性系统

属性系统以后作为独立模块接入。

```text
AttributeComponent
AttributeModifierEffect
AttributeSnapshot
```

当前只保留 `PayloadSchema` 和规则参数型 effect 的边界，不直接实现 AttributeSet。

### 与表现系统

表现系统以后可以按 effect delta 触发。

```text
EffectAdded
EffectRefreshed
EffectRemoved
TagChanged
ComponentChanged
```

当前不做 GameplayCue，但 WorldDelta 要保留足够信息，让 Unity 显示层能判断表现。

### 与网络同步

AOI、可见性、兴趣管理不属于当前总纲。

当前只要求 WorldDelta 能表达 effect 状态变化。后续 AOI 系统决定哪些客户端收到这些 delta。

### 与沙盒测试台

沙盒测试台用于验证配置和规则组合，不成为正式配置源。

```text
Sandbox case
  -> references configId / coord / direction / WorldTag / expected result
  -> does not store component composition
  -> does not define ability/effect
```

## 风险地图

### 配置漂移

风险：

```text
AbilityKind / EffectKind runtime enum 与 Luban enum 不一致
effect_definition 导出后 StreamingAssets 未同步
Fallback provider 与 Luban provider 表现不一致
```

方向：

```text
增加枚举同步检查
增加 provider 读取测试
增加导出产物一致性检查
```

### 规则旁路

风险：

```text
system 直接 world.SetComponent
Debug UI 直接改 tag
ClientMapWorld 直接裁决 effect
```

方向：

```text
所有权威变更走 action/commit
Debug 走服务端 Debug RPC
ClientMapWorld 只接收 delta
```

### tag 误删

风险：

```text
多个 effect 贡献同一 WorldTag
其中一个过期时把共享 tag 删除
```

方向：

```text
RuntimeEffectStore 记录 tag contribution source set
remove effect 时只移除对应来源
source set 为空时才删除 tag
```

### tick 顺序不稳定

风险：

```text
同 tick apply/remove/refresh 顺序不稳定
move commit 与 effect commit 互相影响不确定
```

方向：

```text
定义 tick 阶段
定义 action 排序
定义 commit 排序
测试同 tick 冲突
```

### 客户端错觉

风险：

```text
客户端显示 effect 但服务端没有提交
客户端本地预测 effect 后与服务端冲突
Debug 面板显示的是本地镜像而不是权威状态
```

方向：

```text
Debug 面板标明 server mirror
客户端只展示 WorldDelta 结果
不做客户端预测
```

## 文档层级

当前总纲只负责战略边界。子文档负责落地细节。

```text
runtime-effect-component-plan.md
  -> 定义总体方向、模块边界、系统关系

subgoal/runtime-effect-luban-config.md
  -> 定义 Luban 表和导出校验

subgoal/runtime-ability-model.md
  -> 定义 AbilityDefinition / AbilitySpec / activation

subgoal/runtime-effect-model.md
  -> 定义 EffectDefinition / RuntimeEffectStore / StackPolicy

subgoal/runtime-effect-rules-integration.md
  -> 定义 action / commit / tick 阶段

subgoal/runtime-effect-world-delta.md
  -> 定义同步与客户端镜像

subgoal/runtime-effect-verification.md
  -> 定义测试和手动验证
```

所有子文档必须回到总纲边界，不允许各自发明第二套规则、第二套配置源或第二套客户端裁决。

## 子文档拆分

后续子文档按模块拆，不在总纲里堆实现细节。

### 1. Luban 能力与效果配置

文件建议：

```text
docs/Goal/subgoal/runtime-effect-luban-config.md
```

内容范围：

```text
AbilityKind
EffectKind
ability_definition.xlsx
effect_definition.xlsx
StackPolicy
PayloadSchema
ComponentKind / AbilityKind / EffectKind 同步检查
```

### 2. Shared Ability 模型

文件建议：

```text
docs/Goal/subgoal/runtime-ability-model.md
```

内容范围：

```text
AbilityDefinition
AbilitySpec
AbilityActivationRequest
Ability runtime facade
Ability -> WorldAction / RuntimeEffectSpec
```

### 3. Shared Effect 模型

文件建议：

```text
docs/Goal/subgoal/runtime-effect-model.md
```

内容范围：

```text
EffectDefinition
RuntimeEffectSpec
EffectContext
RuntimeEffectInstance
RuntimeEffectStore
StackPolicy
effect expiration
tag/component contribution source tracking
```

### 4. Rules 接入

文件建议：

```text
docs/Goal/subgoal/runtime-effect-rules-integration.md
```

内容范围：

```text
WorldAction 扩展
EffectCommitProposal
CommitResolver 顺序
StateDrivenRuleExecutionSystem 接入
IntentArbiter 如何读取 effect 贡献的 tag/component
```

### 5. WorldDelta 与客户端镜像

文件建议：

```text
docs/Goal/subgoal/runtime-effect-world-delta.md
```

内容范围：

```text
AddedEffects
RefreshedEffects
RemovedEffects
ChangedTags
ChangedComponents
ClientMapWorld mirror
Debug panel
Unity 显示层边界
```

### 6. 验证与测试

文件建议：

```text
docs/Goal/subgoal/runtime-effect-verification.md
```

内容范围：

```text
Luban 导出验证
Shared/server 测试
Unity TestFramework EditMode
手动端到端验证
Sandbox 验证
不执行 Unity Player build
```

## 阶段关系

阶段不是“临时实现再重构”，而是按模块逐步落地同一个目标架构。

```text
阶段 1: 固定配置定义和 Shared 数据模型
阶段 2: 接入 action/commit 和 RuntimeEffectStore
阶段 3: 接入 WorldDelta 与 ClientMapWorld mirror
阶段 4: 补齐 Debug、Sandbox 和手动端到端验证
阶段 5: 在同一架构下扩展更多 ability/effect
```

每个阶段都必须保持最终边界，不允许为了阶段交付写第二套规则或第二套配置源。

## 总验收方向

这套系统最终要证明：

```text
配置层能定义 ability/effect
服务端能按 tick 激活 ability
服务端能提交 runtime effect
effect 能贡献 tag/component
tag/component 能影响现有规则仲裁
effect 能确定性刷新和过期
多个 effect 共享 tag 时不会互相误删
WorldDelta 能同步 effect 变化
Unity 客户端能镜像和显示 effect
Unity 客户端不权威裁决 effect
```
