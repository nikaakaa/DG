## Context
当前行为层已经有 `ActionSpec`、`ActionRequest`、`ActionClaim`、`ActionArbiter`、`RulePlanner`、`ConflictResolver` 和 `StateDrivenRules`。这些结构让移动类行为看起来走到了统一管线，但实际语义仍然是移动管线中心化：

- `ActionClaim` 以 body/member movement 为主。
- `BehaviorBody` 和 `PortConnectionSystem` 会把多个 entity 归为一个移动 body。
- `MovePlan` 可以包含多个 `BodyMember`。
- `ConflictResolver` 在一个 plan 中提交多个 entity 坐标变化。
- `PendingActionState` 保存 parent/derived 关系，并让 parent 在 derived 成功后 retry。

这与目标行为层不一致。目标不是“所有移动行为统一进 MovePlan”，而是“所有运行时行为统一进 behavior action unit intake”。Move、push、spawn、remove、runtime effect result、机关触发、自动 tick 都应该先成为同一层级的 action unit，再由 action unit 的结果表达 success、fail、handoff、derived、noop 等分支。

## Goals
- 建立 behavior action unit 作为整个行为层唯一入口。
- 让 `WorldAction`、系统触发、pending/handoff、runtime result 都转成 action unit 输入。
- 让一个 action unit 只提交自己的源对象或当前 entity abstraction 的结果。
- 让 pushable 命中产生 handoff：源 action unit 不移动，不等待 retry，派生目标 action unit，并以 handoff 结果结束。
- 移除普通 push 路径的 parent retry 成功语义。
- 将 claim/plan/commit 的默认原子边界从 body members 改成 single action unit result。
- 保持 Shared GameCore 纯 C#，不依赖 Unity/Fantasy。
- 保留端到端手动验证边界，由用户执行 Play Mode / 双客户端验证。

## Non-Goals
- 不处理 port-connected body 的最终抽象。第一阶段可以把连接体作为一个 entity abstraction 留在兼容层，但不得让普通 action unit 继续依赖 body member 原子提交。
- 不引入 AttributeSet、GameplayCue、客户端预测或回滚。
- 不重做 Luban 配置系统。
- 不改变 `RuntimeEffect -> final Component -> Rules` 边界。

## Decisions
- Decision: 新增或重命名运行时输入边界为 behavior action unit intake。
  - Rationale: 当前 `ActionRequest` 更像 move request 适配器，无法表达所有行为来源和结果分支。
- Decision: action unit result 必须显式区分 success、failed、handoff、derived/noop。
  - Rationale: A 推 B 时，A 没有移动，但也不是等待 retry；它应该以 handoff 分支结束。
- Decision: push handoff 只创建目标 action unit，不保留 parent retry。
  - Rationale: 源行为原子不应该跨多个对象完成结果。
- Decision: commit 默认只提交当前 action unit 的源对象或 entity abstraction。
  - Rationale: 多对象 body commit 会把一个行为单元重新变成多个物体的事务。
- Decision: port-connected body 暂时不纳入最终语义重构。
  - Rationale: 用户明确指出连接体可以算一种 entity，之后新开讨论；本变更只防止其语义污染普通行为原子。
- Decision: 已完成的 `refactor-atomic-action-behavior-layer` 不作为最终真相。
  - Rationale: 它完成了部分管线收口，但混入了 parent retry 和 body 原子提交，必须被后续 change 纠正。

## Target Model
```text
External / system sources
  player input
  auto tick
  mechanism trigger
  runtime component result
  push handoff
  debug command

Behavior action unit intake
  source entity / entity abstraction
  spec / primitive / source context
  target / direction / tick / priority

Behavior arbitration
  reads final component / tag / world state
  decides result branch

Result branch
  success: commit this unit's own result
  failed: record stable reason
  handoff: create target action unit and end source unit
  noop: no state change, action ends

Commit
  applies current unit result only
  records dirty / delta
```

## Current Behavior To Replace
- `ActionArbiter` resolves body movement and produces claims for body members.
- `RulePlanner` builds `MovePlan` from accepted body claims.
- `ConflictResolver` loops through plan members and moves multiple entities.
- `PendingActionState` wakes parent retry after child success.
- `StateDrivenRules` writes owner success only after parent unit eventually completes.

## Migration Plan
1. Freeze current broken semantics with failing tests that express the desired handoff model.
2. Introduce behavior action unit result types and action intake naming.
3. Convert player move, auto move, mechanism push and debug move into the action unit intake.
4. Change pushable handling from parent waiting/retry to handoff result.
5. Replace body-member move claims in the default path with single-unit claims.
6. Restrict or isolate `BehaviorBody` / port-connected compatibility so it does not define ordinary action atomicity.
7. Update planner/commit to apply one action unit result at a time.
8. Rewrite Unity EditMode and server verification around handoff, no parent retry, and single-unit commit.

## Risks / Trade-offs
- Risk: Existing port-connected tests will fail or become ambiguous.
  - Mitigation: Mark port-connected body as compatibility/entity abstraction and move final design to a follow-up change.
- Risk: Removing parent retry changes player-visible push behavior.
  - Mitigation: Add explicit handoff result and tests showing source object remains in place while target action unit owns its own movement.
- Risk: Current specs still say body claims and pending push continuation.
  - Mitigation: This proposal modifies those requirements instead of adding a parallel duplicate model.
- Risk: Dirty worktree contains older generated/runtime-effect work.
  - Mitigation: Limit apply-stage edits to listed rule/test/spec paths and do not revert unrelated changes.

## Verification Strategy
- OpenSpec: `openspec validate refactor-unified-behavior-action-entry --strict --no-interactive`
- Shared build: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Server verification: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode:
  - behavior action unit intake tests
  - push handoff tests
  - no parent retry tests
  - single action unit commit tests
  - compatibility guard tests for port-connected body
- Manual Play Mode:
  - 启动服务端和两个 Unity 客户端。
  - 验证 A 推 B 时 A 的 action 以 handoff 分支结束，A 不移动。
  - 验证 B 的独立 action unit 决定 B 是否移动。
  - 验证两个客户端最终 WorldDelta / 坐标一致。
  - 不执行 Unity Player build。
