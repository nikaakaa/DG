## Context
当前 Shared GameCore 已经具备类 ECS 的组件事实层、统一 action spec/request、claim 仲裁、handoff 分支、pending action unit 和 port-connected body claim projection。`PortConnectionSystem` 现在通过 `PortConnectorComponent`、方向和空间邻接收集连通组，这说明 port 的实际模型更接近运行时图连通块，而不是 entity 父子树。

后续要做链接组合体前，先需要把语义口收紧：普通 action unit、connected body view 和未来可能出现的 `CompositeBodyEntity` 不是同一层概念。

## Goals
- 让 action unit subject 成为明确边界：单 entity subject 或 connected body subject。
- 让 connected body view 成为 port 图连通块给规则层的扁平快照。
- 保持普通 push 的 handoff 语义：source action 结束，target action 独立裁决。
- 限定多 member commit 只用于同一个 connected body action result。
- 保持 component/system 风格：component 是事实，system 读取事实并产生 action unit 或 proposal，commit 统一写 world。

## Non-Goals
- 不引入长期 `CompositeBodyEntity`。
- 不把 port 改成父子层级。
- 不把 connected body 的 state split、energy、inventory、body hp 纳入本阶段。
- 不重写 Fantasy 网络协议。
- 不执行 Unity Player build。

## Decisions
- Decision: `PortConnectorComponent` 只表达连接能力。
  - Rationale: port 是图连接属性，不表达父子、等待、控制权或 action 生命周期。
- Decision: connected body view 是本 tick 的规则输入快照。
  - Rationale: 连通块可能随 attach/detach、位置和方向变化而变化，不应默认成为持久 entity。
- Decision: action unit subject 可以是 connected body view。
  - Rationale: port 连通体移动时，对外是一个 action result，对内可以更新多个 member 坐标。
- Decision: 普通 push 保持 handoff。
  - Rationale: 被推动目标应拥有自己的 action unit，source 不等待、不 retry、不把目标纳入自己的原子提交。
- Decision: `CompositeBodyEntity` 延后。
  - Rationale: 只有 body 级长期状态出现时，才需要持久身份壳；提前引入会把连接图误做成实体树。

## Target Model
```text
GameEntity
  final components / tags

PortConnectorComponent
  connection capability

PortConnectionSystem / future PortGraph
  runtime graph connectivity

ConnectedBodyView
  stable root
  flat members
  aggregate capabilities

ActionUnit
  source
  subject: SingleEntity | ConnectedBody
  target / direction / priority / policy

Arbitration
  decides accepted / failed / handoff / noop

Planning
  produces claims or proposals for accepted action subject

Commit
  writes GameWorld once validation passes
```

## Risks / Trade-offs
- Risk: 当前代码名仍使用 `BehaviorBody`、`MovePlan.Members`，容易被读成普通 action 的默认事务。
  - Mitigation: 先补规格和测试名，implementation 阶段再决定是重命名还是加边界测试。
- Risk: connected body 内部多 member commit 看起来和旧 push chain 相似。
  - Mitigation: 测试必须区分 ordinary push handoff 与 connected body member commit。
- Risk: 后续组合体需要长期状态。
  - Mitigation: 本阶段明确只预留 `CompositeBodyEntity`，不把它混入 connected body view。

## Verification Strategy
- OpenSpec: `openspec validate refactor-connected-body-action-subject --strict --no-interactive`
- Shared build: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Server verification: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode:
  - 普通 push handoff 不移动 source
  - handoff target 独立成功、失败或继续 handoff
  - port connected body view 可作为一个 action subject
  - connected body 任一 member 被阻塞则全体不动
  - part 默认不能绕过 connected body subject 单独普通移动
- Manual Play Mode:
  - 用户手动运行服务端权威 Play Mode 和双客户端同步
  - 验证普通 push 与 connected body 移动结果的 WorldDelta 一致
  - 不执行 Unity Player build
