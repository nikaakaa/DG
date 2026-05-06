# Shared Effect 模型子目标

## 定位

本子文档负责定义 DG 运行时效果模型。

Effect 表达 entity 当前受到什么运行时影响。它参照 UE GameplayEffect / GameplayEffectSpec / ActiveGameplayEffect 的职责，但保持 Shared GameCore 纯 C# 数据模型。

## 目标

Effect 层必须回答：

```text
有哪些 effect definition
一次 effect 申请包含什么上下文
已提交 effect 如何存储
同类 effect 如何叠加
effect 如何贡献 tag/component/ability
effect 如何刷新、过期、移除
多个 effect 共享同一 tag 时如何避免误删
```

## 核心对象

### EffectKind

效果类型枚举，与 Luban `EffectKind` 保持一致。

### EffectDefinition

静态效果定义，来自 Luban。

字段方向：

```text
EffectKind Kind
long DefaultDurationTicks
StackPolicy StackPolicy
WorldTag ContributedTags
ComponentKind[] RuntimeComponents
string PayloadSchema
```

### RuntimeEffectSpec

一次待提交效果申请。

字段方向：

```text
EffectKind Kind
long SourceEntityId
long TargetEntityId
long CreatedTick
long? DurationTicks
EffectContext Context
EffectPayload Payload
```

### EffectContext

效果来源事实。

字段方向：

```text
long SourceActionId
long SourceStateId
long SourceEntityId
long TargetEntityId
GridCoord? SourceCoord
GridCoord? TargetCoord
AbilityKind? SourceAbility
string Reason
```

### RuntimeEffectInstance

已提交到 world 的效果实例。

字段方向：

```text
RuntimeEffectId Id
EffectKind Kind
long SourceEntityId
long TargetEntityId
long CreatedTick
long ExpireTick
int StackCount
EffectPayload Payload
```

### RuntimeEffectStore

服务端权威 effect store。

职责：

```text
保存 active effect
按 target entity 查询 effect
按 effect id 查询 effect
按 tick 查找过期 effect
应用 StackPolicy
记录 tag contribution source set
记录 component contribution source set
记录 ability grant source set
产生 dirty effect changes
清理 entity 删除后的 effect
```

## StackPolicy

基础策略：

```text
RejectDuplicate
RefreshDuration
Independent
StackCount
ReplaceByPriority
```

每种策略必须定义：

```text
同类判定 key
同 tick 多来源排序
刷新 expire tick 的规则
StackCount 上限
替换优先级
失败原因
测试断言
```

## Effect 贡献

Effect 可以贡献三类运行时状态：

```text
WorldTag
runtime component
AbilitySpec
```

贡献必须可追踪来源：

```text
WorldTag -> RuntimeEffectId set
runtime component -> RuntimeEffectId set
AbilitySpec -> GrantedByEffectId
```

移除 effect 时只撤回当前 effect 的贡献。共享来源为空后，才移除实际 tag/component。

## 过期

过期由 server tick 驱动。

```text
serverTick >= ExpireTick
  -> ExpireRuntimeEffect action/proposal
  -> CommitResolver
  -> EffectApplicationRegistry remove
  -> RuntimeEffectStore remove
  -> WorldDelta removed effect
```

过期不能由客户端决定。

## EffectApplicationRegistry

`EffectApplicationRegistry` 是 `EffectKind -> apply/remove` 的集中映射。

它可以：

```text
添加 runtime component
移除 runtime component
贡献 WorldTag
撤回 WorldTag contribution
授予 AbilitySpec
撤回 AbilitySpec
```

它不能：

```text
裁决移动
绕过 CommitResolver
修改 ClientMapWorld
直接处理网络同步
```

## 与其他子文档的关系

```text
runtime-effect-luban-config.md
  定义 EffectDefinition 来源。

runtime-ability-model.md
  定义 effect 授予 AbilitySpec 的接口。

runtime-effect-rules-integration.md
  定义 effect request 如何提交。

runtime-effect-world-delta.md
  定义 effect changes 如何同步。

runtime-effect-verification.md
  验证 apply/refresh/remove/expire 和共享 tag。
```
