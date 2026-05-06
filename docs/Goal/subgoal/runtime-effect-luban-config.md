# Luban 能力与效果配置子目标

## 定位

本子文档负责定义运行时能力与效果系统的配置层边界。

它只讨论 Luban 配置、导出、枚举同步和配置读取，不讨论服务端 tick、客户端显示或具体效果实现。

## 目标

配置层必须回答：

```text
有哪些 AbilityKind
有哪些 EffectKind
每个 ability 的默认 tag 需求是什么
每个 ability 会发出什么 action 或 effect request
每个 effect 的持续时间、叠加策略、贡献 tag 和 runtime component 是什么
配置如何导出到 Shared GameCore 与 Unity StreamingAssets
runtime enum 如何与 Luban enum 保持一致
```

## 配置源

正式配置源固定为：

```text
Config/Luban/Defines/gamecore.xml
Config/Luban/Datas/gamecore/ability_definition.xlsx
Config/Luban/Datas/gamecore/effect_definition.xlsx
```

导出入口固定为：

```text
Config/Luban/Run.ps1
```

生成产物固定为：

```text
Shared/DG.GameCore/Config/Generated/LubanTables
Config/Luban/Generated/json
Client/DG_Client/Assets/StreamingAssets/GameConfig
```

## 新增枚举

### AbilityKind

`AbilityKind` 是能力定义的主键枚举。它需要同时存在于：

```text
Config/Luban/Defines/gamecore.xml
Shared/DG.GameCore/Abilities/AbilityKind.cs
cfg.gamecore.AbilityKind
```

三者 name/value 必须一致。

### EffectKind

`EffectKind` 是效果定义的主键枚举。它需要同时存在于：

```text
Config/Luban/Defines/gamecore.xml
Shared/DG.GameCore/Effects/EffectKind.cs
cfg.gamecore.EffectKind
```

三者 name/value 必须一致。

## 新增表

### ability_definition

能力定义表保存静态能力数据。

字段方向：

```text
id
kind
abilityTag
requiredTags
blockedTags
grantedEffects
payloadSchema
```

`grantedEffects` 只表达默认会申请哪些 effect，不保存运行时实例。

### effect_definition

效果定义表保存静态效果数据。

字段方向：

```text
id
kind
defaultDurationTicks
stackPolicy
contributedTags
runtimeComponents
payloadSchema
```

`runtimeComponents` 只表达效果提交后会贡献哪些 runtime component 类型。

## PayloadSchema

`PayloadSchema` 用于给 ability/effect 保留扩展参数结构。

第一层目标是让配置知道“这个 ability/effect 需要什么 payload 形状”，不是立刻实现复杂多态参数系统。

PayloadSchema 需要支持：

```text
无参数
固定整数参数
固定方向参数
固定 tick 参数
固定 tag 参数
未来扩展为组件专用参数
```

PayloadSchema 的详细格式放到本子文档后续章节继续细化。

## StackPolicy

StackPolicy 是配置枚举。

基础策略：

```text
RejectDuplicate
RefreshDuration
Independent
StackCount
ReplaceByPriority
```

每个 `EffectDefinition` 必须明确 stack policy，不允许运行时默认猜测。

## 导出与同步检查

配置层必须增加以下检查：

```text
AbilityKind XML 与 runtime enum 一致
EffectKind XML 与 runtime enum 一致
ability_definition 能由 Luban provider 读取
effect_definition 能由 Luban provider 读取
生成 json 已复制到 Unity StreamingAssets
Fallback provider 不成为正式 ability/effect 数据源
```

现有 `ComponentKind` 同步经验可以复用，但 AbilityKind / EffectKind 要有独立检查，避免后续配置漂移。

## 与其他子文档的关系

```text
runtime-ability-model.md
  读取 AbilityDefinition，并把它绑定到 AbilitySpec。

runtime-effect-model.md
  读取 EffectDefinition，并生成 RuntimeEffectSpec / RuntimeEffectInstance。

runtime-effect-rules-integration.md
  根据 AbilityDefinition / EffectDefinition 进入 action/commit。

runtime-effect-verification.md
  验证 Luban 导出和 provider 读取。
```

## 边界

配置层不保存：

```text
当前谁拥有 AbilitySpec
当前谁拥有 RuntimeEffectInstance
剩余 tick
来源 entity
运行时 stack count
运行时 tag contribution source set
```

这些属于服务端运行时状态。
