## Context
当前权威路径中，连接体直接执行 connected-body subject 的移动时，规划和提交层可以生成全员 `MovePlan`、全员 dirty state 和全员 `WorldDelta`。但“玩家移动撞上连接体后产生推动”的路径会经过 blocked policy、handoff subject、deferred action、pending action unit、accepted action claims，再进入 planning / commit。这个路径中任一环节如果把连接体 subject 折回单个 entry entity，客户端就只能收到部分成员的 delta，表现层自然只能播放部分动画。

客户端 `ClientAnimationLayer` 的边界是观察服务端应用前后的 snapshot 差异，按 changed entity 生成动画事件。客户端显示层不应重新执行连接体解析或推动规则，否则会形成第二套裁决。

## Goals
- 让玩家推动、机关推动、进入推动等产生的 connected body downstream push 都以服务端权威 subject 为准。
- 让成功移动的 connected body 每个成员都出现在同一个权威 `WorldDelta` 中。
- 让每个移动成员都有对应 animation metadata，客户端逐成员播放整体移动表现。
- 让中间连接体在继续传递 push 但自身不移动时，也通过 metadata-only delta 播放 push feedback。
- 保留 source handoff 语义：推动者在 handoff 时不把 downstream body 合并进自己的提交成员集。
- 保留普通 push 链语义：只有尾部实际可移动 subject 产生位移，中间 subject 只传递 push。

## Non-Goals
- 不引入新的事务系统或组合体持久身份。
- 不让客户端根据 port graph 补齐缺失的服务端 delta。
- 不改变 push strength / energy 当前语义；同方向贡献仍只作为保留元信息。
- 不做 Unity Player build。

## Decisions
- Decision: 修复点优先放在服务端规则链路。
  - Rationale: 客户端已经按服务端 delta 逐实体播放动画；只动部分成员说明权威 delta 或 metadata 不完整。
- Decision: handoff subject 必须携带 resolved downstream connected body subject，并在 pending / accepted action 流中保持 subject members。
  - Rationale: `RulePlanner.TryPlanMove(GameWorld, AcceptedAction, ...)` 信任 accepted claims；如果 claims 只有 entry member，后续无法恢复全体成员。
- Decision: 中间 connected body 不做 parent retry。
  - Rationale: 普通 push 链不是整条链一起移动；source stops at handoff，只有 downstream / tail subject 的独立 action unit 能提交自己的移动。
- Decision: `"bounded/deferred-output"` 对 subject members 生成 push feedback metadata，并允许 metadata-only delta 广播。
  - Rationale: 中间 subject 正在传递 push，但没有 changed snapshot；客户端需要服务端明确发送 feedback metadata 才能播放推力动画。
- Decision: 客户端测试只验证“收到全员 delta 就全员动画”和“不根据本地连接体补权威结果”。
  - Rationale: 表现层可以插值和播放，但不能裁决哪些实体应该移动。

## Risks / Trade-offs
- Risk: 修复 subject 传播后，旧测试中只期望一个 changed entity 的玩家 push 场景会失败。
  - Mitigation: 将该测试改成连接体整体移动验收，并保留单实体 push 兼容场景。
- Risk: 同一连接体多 contact 可能重复生成 child unit。
  - Mitigation: 继续使用 resolved subject key 做 dedupe，只在 downstream subject 不同时 fanout。
- Risk: 全员 metadata 与全员 delta 不一致会导致动画缺失。
  - Mitigation: 服务端验证同时断言 `ChangedEntities` 和 `AnimationMetadata` 覆盖同一组移动成员。
- Risk: metadata-only delta 增加一类没有 changed entity 的同步包。
  - Mitigation: 仅在 `AnimationMetadata` 非空时广播，并由客户端动画层按 metadata 生成 impulse feedback，不改变本地权威坐标。

## Validation Plan
- 运行 `openspec validate fix-connected-body-push-delta-animation --strict --no-interactive`。
- 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- 运行 Unity TestFramework EditMode，覆盖 `ClientAnimationLayerTests` 和 `ClientMoveNetworkRuntimeTests` 的连接体推动动画场景。
- 用户手动 Play Mode 验证：直接推动 downstream connected body 时，每个实际移动成员同步移动并播放移动动画；普通连续 push 链中间连接体不移动但播放 push feedback，尾部实际移动。
- 用户手动双客户端验证观察端在不移动时也看到相同 push feedback、尾部移动动画和最终坐标。
