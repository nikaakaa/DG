# RemoveEffectRunner (RunnerId = `remove_effect_runner`)

实现 `remove_runtime_effect` BehaviorId，负责把过期 / 显式移除的 runtime effect 转成 `CommitProposal.RemoveRuntimeEffect`。

当前阶段 M3 仅落最小职责:把 effect 过期入口的"生成 remove_runtime_effect candidate"的工厂方法集中在这里。M6 阶段会把 `RemoveRuntimeEffectCommitHandler` 重组进 Runner 内部三段。

## 状态枚举

```
Idle       -> 未启动
Proposing  -> Enter：直接构造 CommitProposal.RemoveRuntimeEffect
Settling   -> Exit：proposal 投入 CommitResolver
Completed  -> 一帧结束（CostTicks=1）
```

## 合法 transition

```
Idle      -> Proposing      (effect expired / 显式 remove request)
Proposing -> Settling       (proposal 入队)
Settling  -> Completed
```

## Enter / Tick / Exit emit 的 fact

| Phase    | 必发 ActionFact            | 备注                                          |
|----------|----------------------------|-----------------------------------------------|
| Enter    | (none)                     |                                               |
| Tick     | (none)                     |                                               |
| Exit     | `RuntimeEffectRemoved`     | 成功后必发，包含 effectId / targetEntityId     |

## 默认 claim

- channel: `status`
- mode: `Shared`
- CostTicks: `1`

## Internal services

- 当前阶段仅 `CommitProposal.RemoveRuntimeEffect` 工厂。
- M6 后内化 `RemoveRuntimeEffectCommitHandler`。
