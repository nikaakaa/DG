## Context
`docs/Goal/subject-and-composite-entity.md` 将动作结算对象收敛为命中入口实体、临时 connected body view 和未来的 `CompositeEntity`。当前实现应以 `ActionSubjectKind.HitEntity / ConnectedBodyIfAny` 表达 subject policy；旧的 `SingleEntity / ConnectedBody` 只能作为迁移兼容别名存在，不能继续作为文档语义。

项目级约束已经明确：`ActionSpecId` 只用于查找配置，规则层不得根据 action 名字硬编码行为；tag 只表达规则输入或过滤条件，不替代 `ActionSpec` 策略字段。

当前问题不是缺少 port graph，也不是需要持久组合实体，而是 subject 选择还没有被足够明确地建模为 `ActionSpec` policy。`connected_body_move` 当前能表达 connected body subject，但它容易让后续实现继续把“是否 connected body 结算”绑定到 action 名字。

## Goals
- 让 subject 选择由 `ActionSpec` policy 表达，而不是由 action 名字、entity 名字或 tag 组合推断。
- 让需要 connected body 结算的现有行为配置通过 subject policy 解析 connected body subject。
- 保持 `SubjectResolver` 纯解析职责，不创建 plan、handoff 或 world mutation。
- 保持普通 non-port entity 和普通 single entity subject 行为不变。

## Non-Goals
- 不实现 `CompositeEntity`。
- 不实现 `CompositeEntityIfAny` 运行时 policy。
- 不重做 push chain planning、pending lifecycle 或 conflict commit。
- 不删除所有 `connected_body_move` 兼容路径。
- 不尝试 Unity Player build。

## Decisions
- Decision: 复用并收紧现有 `ActionSubjectKind`，不要新增一套平行枚举。
  - `SingleEntity` 对应 `HitEntity` 语义。
  - `ConnectedBody` 对应 `ConnectedBodyIfAny` 语义。
- Decision: `mechanism_push`、`connected_body_move` 等字符串只作为 `ActionSpecId`，规则层不得根据这些名字决定 subject。
- Decision: 是否展开 connected body subject 只读取解析后的 `ActionSpec.subjectPolicy`。
- Decision: `connected_body_move` 只作为兼容过渡；新增普通行为不得通过复制 `connected_body_xxx` action 名来获得 subject 能力。
- Decision: subject resolver 只返回本次 action subject，不生成 `MovePlan`，不判断 blocker，不创建 handoff，不修改 `GameWorld`。
- Decision: `CompositeEntityIfAny` 只记录为未来扩展边界，本 change 不实现。

## Relationships
- `refactor-port-linked-body-foundation` 负责 port graph 和 connected body view 的底层语义。
- `refactor-body-push-chain-planning` 负责 body-to-body push chain、handoff 和 pending chain 规划。
- 本 change 只负责 action entry 到 subject 的 policy 化选择，避免和上述两个 change 重复修改 commit 或 chain 语义。

## Risks / Trade-offs
- Risk: 如果直接重命名 `ActionSubjectKind`，会造成测试和现有调用大面积改动。
  - Mitigation: 第一阶段可以保留类型名，只收紧 spec 语义和默认 `ActionSpec` 配置。
- Risk: 只改某个 `ActionSpecId` 的配置容易被误读成名字特判。
  - Mitigation: spec 和测试必须证明规则层只读取 `ActionSpec.subjectPolicy`，不读取 action 名字分支。
- Risk: 与 active body push chain proposal 同时修改 connected body 行为。
  - Mitigation: 本 change 不修改 chain、retry、commit requirement，只规定 subject selection boundary。

## Open Questions
- 哪些现有 `ActionSpecId` 应该配置为 `ConnectedBodyIfAny`，需要按玩法语义逐条确认。
- `player_push` 是否也应使用 `ConnectedBodyIfAny`，需要按玩法语义单独确认。
- `connected_body_move` 何时删除或隐藏，需要在兼容迁移完成后另开 cleanup change。
