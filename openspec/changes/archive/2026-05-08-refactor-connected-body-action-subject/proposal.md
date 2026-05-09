# Change: 收口原子行动主体与链接组合体前置边界

## Why
当前原子行动、claim 仲裁、handoff 和 port-connected body 已经基本完成，但后续准备做链接组合体时，需要先把“普通 action unit”和“由 port 图形成的 connected body action subject”边界写成明确规格。否则 `MovePlan.Members`、`BodyResolver` 和 `PortConnectionSystem` 容易再次被理解成普通移动的多实体事务，或被提前扩展成父子树式 `CompositeBodyEntity`。

## What Changes
- 明确 action unit 的主体可以是单 entity 或 connected body view，但普通 push 不因为命中目标而变成多成员原子提交。
- 明确 port 是 entity 的连接能力，运行时连接通过图连通块形成 connected body view，规则层消费扁平 view，不维护 entity 树。
- 明确 `CompositeBodyEntity` 不进入本阶段实现；只有未来需要组合体长期状态时才作为新的 proposal 引入。
- 明确 connected body 的多 member commit 只代表同一个 connected body action result 的内部写入，不代表普通 action 可以提交无关 entity。

## Impact
- Affected specs:
  - `claim-driven-action-arbitration`
  - `shared-gamecore-entity-rules`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Commit/ConflictResolver.cs`
  - `Shared/DG.GameCore/Rules/Connectivity/PortConnectionSystem.cs`
  - Unity TestFramework EditMode tests
  - `Server/Tests/AuthoritativeMoveVerification`
- Non-goal:
  - 不实现新的 `CompositeBodyEntity`
  - 不重做 Unity UI
  - 不执行 Unity Player build
