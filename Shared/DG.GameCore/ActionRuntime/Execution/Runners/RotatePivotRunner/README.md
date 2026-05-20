# RotatePivotRunner (RunnerId = `rotate_pivot_runner`)

实现 `rotate_pivot_success` 与 `rotate_pivot_bounce` 两个 BehaviorId,把 push 向量诱发的 body 旋转 (pivot-driven rotate) 集中处理:解算 torque、规划 sweep、决定 success 还是 bounce、产出 BehaviorInstance 与对应的 ActionFact。

底层 sweep / contact / bounce timing 解算由 `RotatePivotResponseProcessor` 承担,Runner 在其上加 RunnerId 常量 + 默认 claim 元数据。

## 状态枚举

```
Idle              -> 未启动
GroupCollecting   -> Enter:扫描候选 push request,按 (readyTick, bodyKey) 聚合
PivotResolving    -> Tick:body 解算 + pivot 计数 + torque 解算
SweepPlanning     -> Tick:RotateSweepPlanner.TryPlan 算出 BodyMember + sweptCells
ContactProbing    -> Tick:检索 sweptCells 上的 blocker,决定 success vs bounce
Rotating          -> Tick(success):无阻挡,整体随 pivot 旋转一格
Bouncing          -> Tick(bounce):有阻挡,沿圆周走到 contact tick 后反弹回起点
DeferredOutput    -> Tick(bounce):向 blocker 发起 derive push deferred
Completed         -> end_tick:effective_cost_ticks 用尽
Failed            -> pivot 数 != 1 / zero torque / sweep 失败 / handoff 关
```

## 合法 transition

```
Idle             -> GroupCollecting   (admission 通过)
GroupCollecting  -> PivotResolving    (找到至少一个候选 group)
GroupCollecting  -> Failed            (no candidate body)
PivotResolving   -> SweepPlanning     (pivot 数 == 1 且 torque != 0)
PivotResolving   -> Failed            ("rotate-pivot-invalid" / "rotate-pivot-zero-torque")
SweepPlanning    -> ContactProbing    (sweep 解算成功)
SweepPlanning    -> Failed            ("missing position" / "rotate-pivot-duplicate-target")
ContactProbing   -> Rotating          (无 contact)
ContactProbing   -> Bouncing          (有 contact)
Rotating         -> Completed         (CostTicks 用尽)
Bouncing         -> DeferredOutput    (到 contact tick,产 derive push)
Bouncing         -> Failed            ("rotate-pivot-blocked" / "rotate-pivot-handoff-disabled")
DeferredOutput   -> Completed         (回到起点)
```

## Enter / Tick / Exit emit 的 fact

| Phase              | 必发 ActionFact                                    | 备注                                                             |
|--------------------|----------------------------------------------------|------------------------------------------------------------------|
| Enter (success)    | `RotateStarted`                                    | resultKind = Success,members + 0 impacts                         |
| Enter (bounce)     | `RotateStarted`                                    | resultKind = Bounce,members + impacts(blockers)                  |
| Tick (bounce@contact)| `RotateContacted`                                | 每个 blocker 一条,带 PushDirection / impact                      |
| Exit               | `RotateCompleted`                                  | internal completion fact,带 subjectIds                           |

bounce 路径的 `RotateContacted` 走 `BehaviorScheduledOutput` 在 `contact_tick` 投递,Completed 则在 `end_tick` 统一释放。

## 默认 claim

- channel: `movement`
- mode: `Exclusive` (整组 body cells + body subject ids 都进 claim)
- CostTicks: `1` (实际 effective_cost_ticks 由 contact 处的 progress 决定)

`ReservationScope` 默认即 (Movement, Exclusive),Runner 通过 `RotatePivotRunner.DefaultClaimChannel/Mode/CostTicks` 显式暴露规约。

bounce 路径下的 `effective_cost_ticks = contact_out_ticks * 2`,但默认 claim 占用窗口跨整个 `[serverTick, endTick]`,确保被打断或被抢占时也能完整释放。

## Internal services

- `RotatePivotResponseProcessor` (旧入口,Runner 内化为唯一持有者)
- `BodyResolver` / `BodyCapabilityResolver`
- `RotateSweepPlanner`
- `BlockingSpatialQuery` (`world.TryGetFirstBlockingAt`)
- `BlockedOutcomeUtility` (deferred dedupe / subject key)
