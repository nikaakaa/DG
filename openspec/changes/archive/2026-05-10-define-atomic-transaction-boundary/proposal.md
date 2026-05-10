# Change: 定义原子事务与涌现运动边界

## Why

当前 multi-contact / connected body push 调试暴露出一个更底层的问题：闭环结构和多路径收敛会让 pending push chain 试图在一次事务里继续展开，最终把可涌现的持续行为误判为 `push chain cycle` 或推向全局链条求解。

DG 需要先明确“单次 action / 单 tick 事务必须有限”和“持续运动来自多个 tick 的重复原子提交”，否则后续 convergence、闭环装置和 push 叠加规则会继续互相混淆。

## What Changes

- 明确一次原子事务的边界：输入有限、subject 有限、同一 subject 同 tick 最多消费一次、commit 有限。
- 定义同 tick push 可以叠加，但反馈 push 最早在 `tick + cost` 生效。
- 定义闭环装置不由 pending chain 求完整终点，而由 world tick 根据结构化 deferred output 反复生成有限 action input 来涌现持续输出。
- 明确“涌动增幅 push 机器”第一阶段是 `period = cost` 的周期输出装置，不是强度增长装置；未来是否成为增幅器必须由显式 strength policy 决定。
- 区分重复命中同一 subject 的 convergence、不同 subject 部分重叠的 unsafe overlap、真正回到祖先的 cycle。
- 将 push propagation 从 `PendingRuleStates` / parent-child handoff 语义中移出；本 change 不为未来等待型 action 保留 push 链条。
- 未来如果出现确实需要 parent 等待 child result 的 action，必须另起 proposal，并证明它是有界事务，不能复用当前这条会被闭环打爆的链条。
- 要求 deferred output 使用显式结构化字段，不允许通过 action 名字、entity 名字、tag 组合或类似 `mechanism_push` 的硬编码分支推断行为。

## Impact

- Affected specs:
  - `atomic-action-behavior-layer`
  - `claim-driven-action-arbitration`
  - `shared-gamecore-entity-rules`
- Affected code:
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`
  - `Shared/DG.GameCore/Rules/Pending/PendingRuleStates.cs`
  - `Shared/DG.GameCore/Rules/Planning/BodyCapabilityResolver.cs`
  - `Shared/DG.GameCore/Rules/Planning/RulePlanning.cs`
  - `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
  - Unity TestFramework EditMode tests under `Client/DG_Client/Assets/Tests/Editor/ClientWorld/`
