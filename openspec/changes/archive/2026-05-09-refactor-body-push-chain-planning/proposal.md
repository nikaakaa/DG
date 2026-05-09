# Change: 规划组合体推动链

## Why
当前 port linked body 已经能把多个 entity 作为一个 connected body 推动，但当一个 connected body 的目标格又被另一个 pushable connected body 阻挡时，现有规则还没有清晰表达“组合体推组合体”的传播语义。

如果不先定义这一层，横排组合体会容易退回成普通单 entity push chain，导致最右侧 member 被单独推出、body 被拆开，或者把整条链错误合成一个超大事务。

## What Changes
- 把 connected body push 传播定义为 action unit 派生链，而不是同 tick 全链求解。
- 修改现有“Non-Transitive Push Blocking”边界，允许受控的嵌套派生 push。
- 明确每个 connected body 是独立 action subject，commit 只覆盖该 subject 的 members。
- 增加 pending push chain 的安全边界：最大深度、visited body、防循环、一个 parent 同时只等待一个 child。
- 明确 `PushableComponent` 仍然只是入口能力，不传播到整个 body。
- 明确组合体推动链依赖 `refactor-port-linked-body-foundation` 的 connected body view 和 body capability resolver。

## Impact
- Affected specs:
  - `atomic-action-behavior-layer`
  - `claim-driven-action-arbitration`
  - `shared-gamecore-entity-rules`
- Affected code:
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Shared/DG.GameCore/Rules/Planning/BodyCapabilityResolver.cs`
  - `Shared/DG.GameCore/Rules/Connectivity/PortConnectionSystem.cs`
  - `Client/DG_Client/Assets/Tests/Editor/ClientWorld/DataDrivenRuntimeActionTests.cs`
  - `Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveWorldVerification.cs`

## Dependencies
- `refactor-port-linked-body-foundation` must provide stable connected body view, push entry resolution, member movability resolution, and all-or-nothing connected body commit before this change is implemented.

## Non-Goals
- 不引入持久 `CompositeBodyEntity`。
- 不实现轮胎、车辆、模块识别、驾驶权或 body 级长期状态。
- 不把 port graph 改成 parent-child 树。
- 不把一整条 push chain 合并为一个同 tick 大事务。
- 不执行 Unity Player build。
