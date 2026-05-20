# RemoveRunner (RunnerId = `remove_runner`)

实现 `remove_entity` 与 `debug_remove` 两个 BehaviorId 的落地。

## 状态枚举

```
Idle       -> 未启动
Resolving  -> Enter：定位 entity 与当前 coord
Proposing  -> Tick：产出 CommitProposal.Delete
Settling   -> Exit：MoveResult 写回
Completed  -> 一帧结束（CostTicks=1）
Failed     -> entity not found
```

## 合法 transition

```
Idle      -> Resolving         (admission 通过)
Resolving -> Proposing         (entity 存在)
Resolving -> Failed            ("entity not found")
Proposing -> Settling          (CommitProposal 入队)
Settling  -> Completed
```

## Enter / Tick / Exit emit 的 fact

| Phase    | 必发 ActionFact   | 备注                              |
|----------|-------------------|-----------------------------------|
| Enter    | (none)            |                                   |
| Tick     | (none)            |                                   |
| Exit     | `EntityRemoved`   | 成功后必发，包含 entityId / coord  |

`debug_remove` 走同 emit 集合，但 channel 是 `debug + interaction`。

## 默认 claim

- channel: `interaction`（debug_remove 为 `debug + interaction` 组合 claim）
- mode: `Exclusive`
- CostTicks: `1`
