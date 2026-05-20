# ApplyEffectRunner (RunnerId = `apply_effect_runner`)

实现 `apply_runtime_effect` BehaviorId，把 effect spec 落到目标 entity 上。

## 状态枚举

```
Idle           -> 未启动
ResolvingContext -> Enter：构造 ActionContext，校验 EffectSpec
TargetSelecting  -> Tick：用 TargetingSystem 解算 target data
ProposingEffect  -> Tick：每个 target 产 CommitProposal.AddRuntimeEffect
Settling         -> Exit：MoveResult 写回
Completed        -> 一帧结束（CostTicks=1）
Failed           -> EffectSpec 缺失 / entity not found / target resolution 失败
```

## 合法 transition

```
Idle             -> ResolvingContext   (admission 通过)
ResolvingContext -> TargetSelecting    (ActionContext 与 EffectSpec OK)
ResolvingContext -> Failed             ("unknown effect spec" / "entity not found")
TargetSelecting  -> ProposingEffect    (target data 解算成功)
TargetSelecting  -> Failed             (target selector 报错)
ProposingEffect  -> Settling           (每个 target 入队 AddRuntimeEffect)
Settling         -> Completed
```

target data 为空时回退到自瞄 (`ActionTargetData.Self`)。

## Enter / Tick / Exit emit 的 fact

| Phase    | 必发 ActionFact          | 备注                                                          |
|----------|--------------------------|---------------------------------------------------------------|
| Enter    | (none)                   |                                                               |
| Tick     | (none)                   |                                                               |
| Exit     | `RuntimeEffectApplied`   | 成功后必发，包含 effectSpecId / targetEntityId / stackKey      |

## 默认 claim

- channel: `status`
- mode: `Shared`
- CostTicks: `1`

## Internal services

- `TargetingSystem` (使用 `TargetSelectorRegistry.CreateDefault()`)
- `IGameConfigProvider.TryGetEffectSpec`
- `CommitProposal.AddRuntimeEffect`
