## Context
当前正式规格已经要求 port 作为连接能力，而不是 entity hierarchy；基础 port-connected movement 也已经明确不创建持久 `CompositeBodyEntity`。下一步要把底层 linked body 语义补完整，使后续模块装配能建立在稳定基础上。

## Goals
- 保持 flat entity + port graph + connected body view。
- 把 push entry 和 body movement 分开。
- 保持 component 是 member 事实，不因链接自动共享。
- 让规则层通过 body capability 聚合结果决策。
- 让 connected body commit 保持一个 action result、多个 member delta。

## Non-Goals
- 不实现持久 `CompositeBodyEntity`。
- 不实现轮胎、车辆、模块识别。
- 不把 port 改成父子关系。
- 不让 part 在 connected body action 中绕过 body 单独移动。
- 不执行 Unity Player build。

## Decisions
- Decision: `PortConnectorComponent` 只表达连接能力。
  - Rationale: port 是图边能力，不应该携带 push、control、owner、action lifecycle。
- Decision: `PushableComponent` 是 member 入口能力。
  - Rationale: A 有 Pushable、B 无 Pushable 且 A-B 连接时，推 A 可以尝试推动 body；推 B 不应该创建 push action。
- Decision: movement permission 是 body 聚合能力。
  - Rationale: 被 push 的入口合法后，仍然必须检查所有将移动的 member 是否允许跟随移动。
- Decision: linked body 不传播 component。
  - Rationale: component 是 entity 当前事实，传播会污染 delta、snapshot、调试和后续 detach 语义。
- Decision: body capability resolver 是规则入口。
  - Rationale: push entry、movability、blocking、control 的聚合策略会继续增长，不能塞进 port graph。
- Decision: cache 是优化，不是语义来源。
  - Rationale: 第一版可以直接解析；后续再用 dirty port/position/direction/entity changes 驱动 cache。

## Target Model
```text
GameEntity
  final components / tags

PortConnectorComponent
  local ports / compatibility input

PortConnectionSystem
  graph connectivity from final components, position, direction

ConnectedBodyView
  bodyId
  stableRootEntityId
  members
  entryEntityId

BodyCapabilityResolver
  ResolvePushEntry(body, entry)
  ResolveMovability(body)
  ResolveBlocking(body)
  ResolveControl(body)

ActionUnit
  subject: SingleEntity | ConnectedBody
  entryEntityId
  bodyId

Planning
  produces claims for all moved members

Commit
  succeeds only when every required claim is valid
```

## Push Semantics
```text
A: Pushable
B: not Pushable
A -- B
```

推 A：
- A 是合法 push entry。
- 系统解析 A-B 为 connected body。
- 系统检查 A/B 是否都能跟随移动。
- 通过后 A/B 一起移动。

推 B：
- B 不是合法 push entry。
- 系统不创建 push action。
- A/B 都不动。

## Risks / Trade-offs
- Risk: `PushableComponent` 被误读为 body-wide 能力。
  - Mitigation: 规格和测试必须覆盖 linked pushable entry 与 non-pushable entry 的差异。
- Risk: capability 聚合和 port graph 混在一起。
  - Mitigation: port graph 只返回 connectivity，body capability resolver 单独负责聚合。
- Risk: cache 提前变成状态真相源。
  - Mitigation: cache 必须可由 final component/world state 重建。

## Verification Strategy
- OpenSpec: `openspec validate refactor-port-linked-body-foundation --strict --no-interactive`
- Shared build: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Server verification: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- Unity TestFramework EditMode:
  - linked pushable entry pushes whole body
  - linked non-pushable entry rejects push
  - linked body does not propagate `PushableComponent`
  - blocked movement permission rejects whole body
  - external blocker rejects whole body all-or-nothing
  - port cycle resolves stable body root/members
- Manual Play Mode:
  - 用户手动运行服务端权威 Play Mode 和双客户端同步
  - 验证 linked push 的 WorldDelta 在服务端和两个客户端一致
