# Shared Ability 模型子目标

## 定位

本子文档负责定义 DG 运行时能力模型。

Ability 表达 entity 当前能发起什么行为。它参照 UE GameplayAbility / GameplayAbilitySpec 的职责，但保持 Shared GameCore 纯 C# 数据模型。

## 目标

Ability 层必须回答：

```text
某个 entity 当前拥有哪些 ability
ability 来自静态配置还是 runtime effect 授予
ability 激活时需要检查哪些 tag
ability 激活后产生 WorldAction 还是 RuntimeEffectSpec
ability 激活失败如何表达
ability 随 entity 或 effect 生命周期如何清理
```

## 核心对象

### AbilityKind

能力类型枚举，与 Luban `AbilityKind` 保持一致。

### AbilityDefinition

静态能力定义，来自 Luban。

字段方向：

```text
AbilityKind Kind
WorldTag AbilityTag
WorldTag RequiredTags
WorldTag BlockedTags
EffectKind[] GrantedEffects
string PayloadSchema
```

### AbilitySpec

entity 当前拥有的能力实例。

字段方向：

```text
AbilityKind Kind
long OwnerEntityId
WorldTag GrantedByTag
RuntimeEffectId? GrantedByEffectId
long? ExpireTick
AbilityPayload Payload
```

### AbilityActivationRequest

一次能力激活请求。

字段方向：

```text
AbilityKind Kind
long SourceEntityId
long SourceActionId
long ServerTick
GridCoord? TargetCoord
long? TargetEntityId
AbilityPayload Payload
```

### AbilityActivationResult

一次能力激活结果。

结果类型：

```text
Accepted
Rejected
EmittedWorldAction
EmittedRuntimeEffectSpec
```

失败原因：

```text
SourceEntityMissing
AbilityMissing
MissingRequiredTag
BlockedByTag
InvalidTarget
TargetOutOfRange
CooldownBlocked
CostBlocked
InvalidPayload
```

当前总纲不实现 cooldown/cost，但结果模型保留位置。

## Ability 来源

AbilitySpec 有三种来源：

```text
静态 archetype
runtime effect 授予
debug action 授予
```

静态来源来自实体出生配置。runtime 来源必须记录 `GrantedByEffectId`，方便 effect 过期时撤回 ability。

## Ability 激活边界

Ability 激活只能产生：

```text
WorldAction
RuntimeEffectSpec
AbilityActivationResult
```

Ability 激活不能直接：

```text
world.SetComponent
world.AddTag
world.RemoveTag
修改 PositionComponent
修改 RuntimeEffectStore
修改 ClientMapWorld
```

## Ability 与 BehaviorIntent

现有 `BehaviorIntent` / `IntentArbiter` 是规则仲裁层的一部分。

Ability 可以产生 `WorldAction`，由现有主路继续转成 `BehaviorIntent`。

```text
AbilityActivationRequest
  -> WorldAction
  -> BehaviorIntent
  -> IntentArbiter
```

Ability 不替代 `BehaviorIntent`。

## Ability 与 Effect

Ability 可以申请 effect。

```text
AbilityActivationRequest
  -> RuntimeEffectSpec
  -> EffectPlanner
  -> EffectCommitProposal
```

Ability 不保存 effect 生命周期。effect 生命周期由 `RuntimeEffectStore` 管理。

## 生命周期

AbilitySpec 的清理规则：

```text
entity 删除时清理所有 AbilitySpec
GrantedByEffectId 对应 effect 移除时清理授予的 AbilitySpec
ExpireTick 到达时清理临时 AbilitySpec
```

静态 AbilitySpec 跟随 entity 生命周期。

## 与其他子文档的关系

```text
runtime-effect-luban-config.md
  定义 AbilityDefinition 的配置来源。

runtime-effect-model.md
  定义 effect 授予 ability 时如何记录来源。

runtime-effect-rules-integration.md
  定义 AbilityActivationRequest 如何进入 tick/action。

runtime-effect-verification.md
  验证 ability 激活、失败、授予和清理。
```
