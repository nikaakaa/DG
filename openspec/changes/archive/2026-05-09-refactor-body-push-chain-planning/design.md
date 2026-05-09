## Context
已有设计把 port 定义为连接能力，把运行时连接解析成 flat connected body view，并推迟持久 `CompositeBodyEntity`。当前缺口是 body 与 body 相互阻挡时，推动传播应继续走 action unit / pending handoff，而不是退回 execution 层特殊分支，也不是让父级在子级成功后继续跟进移动。

## Goals
- 组合体推动组合体时保持 body 不拆分。
- 普通 push chain 和 connected body push chain 使用同一套 handoff action unit 机制。
- 每个 action unit 只提交自己在当前步骤实际要移动的 subject。
- 链式传播可以跨多个 body，但每一步仍然由独立 ready tick、仲裁、规划、提交决定，触发者自己不因后续成功而移动。
- 失败原因稳定，避免循环、无限 pending、重复 active handoff。

## Non-Goals
- 不做同 tick 全链求解。
- 不做持久 body identity。
- 不做 body 级血量、能量、背包、控制权。
- 不新增玩家操作优先级模型。
- 不改变 port 兼容连接规则。

## Decisions
- Decision: 组合体推动链使用 handoff action unit。
  - Rationale: 当前玩法语义是传递推力，而不是递归让位；P 命中 `[A-B]` 后 P 留在原地，`[A-B]` 命中 `[C-D]` 后 `[A-B]` 留在原地。
- Decision: `connected_body_move` 或等价 body subject action 可以 handoff 到下一个 blocker action。
  - Rationale: 被 body 阻挡和被单 entity 阻挡都是 blocked policy 的输入差异，不应该在 commit 层拆分 body。
- Decision: 每个 body action unit 只提交自己在当前步骤实际要移动的 connected body members。
  - Rationale: B 推 C 时，B 的 action 不应把 C 加进自己的 member commit；如果 C 可以继续传力，则最终只有链尾可进入空位的 subject 移动。
- Decision: push entry 仍然是 touched member 的能力。
  - Rationale: A-B body 中只有 A 有 `PushableComponent` 时，命中 A 可以推动 body，命中 B 不可以把 A 的能力传播过来。
- Decision: pending chain 保存 body-aware chain metadata。
  - Rationale: 只保存 front entity 不足以表达 subject body、entry entity、visited body 和 handoff unit。
- Decision: 链安全是语义边界，不是性能优化。
  - Rationale: 最大深度、visited body、防重复 handoff 是防止规则失控的必要条件，不能等 cache 优化再补。

## Target Model
```text
ActionUnit
  ownerActionId
  actionUnitId
  handoffFromUnitId?
  subjectKind: SingleEntity | ConnectedBody
  subjectEntityIds
  entryEntityId
  direction
  readyTick

BlockedPolicy
  hit blocker
  resolve blocker subject
  resolve blocker push entry
  create handoff action unit
  source unit enters handoff

PendingChain
  root owner action
  source unit
  active handoff unit
  visited subject ids
  depth
  timeout tick

Commit
  commit only accepted unit subject
  child success completes owner result at current root position
  source unit does not retry movement
```

## Example
```text
P -> [A-B] -> [C-D] -> empty
```

第一 tick：
- P 的 action 命中 A。
- A 是 `[A-B]` 的合法 push entry。
- P 留在原地。
- `[A-B]` 产生 handoff body action。

第二 tick：
- `[A-B]` 的 body action 命中 C。
- C 是 `[C-D]` 的合法 push entry。
- `[A-B]` 留在原地。
- `[C-D]` 产生 handoff body action。

第三 tick：
- `[C-D]` 成功移动到右侧空位。
- owner action 结束为 success，但 P 的 final coord 仍是当前位置。

最终结果：
- P 不动。
- `[A-B]` 不动。
- 只有 `[C-D]` 被推出去。
- 整个过程中，A/B 不会被拆成普通 chain，C/D 也不会被拆成普通 chain。

## Failure Rules
- blocker member 不是合法 push entry 时，当前 unit failed，owner action failed。
- blocker body 任一 member movement permission 失败时，当前 unit failed。
- blocker body 的移动目标仍被不可推动 blocker 阻挡时，当前 unit failed。
- chain depth 超过上限时，当前 unit failed。
- chain 访问到已在当前链中的 subject 时，当前 unit failed。
- source 已有 active handoff 时，不再创建第二个 handoff。
- pending 超时后 owner action failed。

## Risks / Trade-offs
- Risk: 行为变慢，每个 body propagation 需要多个 ready tick。
  - Mitigation: 这是当前 action unit 语义的可见规则，不在本 proposal 中优化成同 tick 全链。
- Risk: pending state 临时字段继续膨胀。
  - Mitigation: 实现任务要求整理成显式 chain metadata，而不是继续依赖 front entity。
- Risk: body id 不持久导致 visited 判断不稳定。
  - Mitigation: visited subject 使用当前 tick 可重建的 stable root 或排序 member key，不创建持久 `CompositeBodyEntity`。
- Risk: 与未完成的 port foundation 重叠。
  - Mitigation: 本 change 只在 port foundation 的 connected body view 和 capability resolver 之上扩展，不重新定义 port graph。

## Verification Strategy
- OpenSpec: `openspec validate refactor-body-push-chain-planning --strict --no-interactive`
- Shared build: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Server verification: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode:
  - body pushes body without splitting members
  - multi-body chain handoffs until only tail body moves
  - non-pushable entry rejects body propagation
  - cycle guard rejects repeated body subject
  - depth guard rejects too-long chain
  - owner succeeds at its current position after tail movement
- Manual Play Mode:
  - 用户手动运行服务端权威 Play Mode 和双客户端
  - 验证横排多个 connected body 被推动时，只有最右侧 body 被推出去，服务端和两个客户端最终 WorldDelta 一致
