# SpawnRunner (RunnerId = `spawn_runner`)

实现 `spawn_entity` 与 `debug_spawn` 两个 BehaviorId 的落地。

## 状态枚举

```
Idle      -> 未启动
Validating-> Enter：校验 target coord
Proposing -> Tick：产出 CommitProposal.Create
Settling  -> Exit：MoveResult 写回
Completed -> 一帧结束（CostTicks=1）
Failed    -> 缺少 target / 非法 coord
```

## 合法 transition

```
Idle       -> Validating       (admission 通过)
Validating -> Proposing        (TargetCoord 存在)
Validating -> Failed           ("missing target")
Proposing  -> Settling         (CommitProposal 入队)
Settling   -> Completed
```

## Enter / Tick / Exit emit 的 fact

| Phase    | 必发 ActionFact   | 备注                                |
|----------|-------------------|-------------------------------------|
| Enter    | (none)            |                                     |
| Tick     | (none)            |                                     |
| Exit     | `EntitySpawned`   | 成功后必发，包含 archetypeId / coord |

`debug_spawn` 走同 emit 集合，但 channel 是 `debug + interaction`。

## 默认 claim

- channel: `interaction`（debug_spawn 为 `debug + interaction` 组合 claim）
- mode: `Exclusive`
- CostTicks: `1`

## Internal services

`SpawnRunner` 内部直接构造 `CommitProposal.Create`,不持有额外 policy service。
