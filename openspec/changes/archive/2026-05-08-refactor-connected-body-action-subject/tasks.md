## 1. Proposal Validation
- [x] 1.1 阅读 `proposal.md`，确认 scope 只收口 action subject 和 port connected body 前置边界。
- [x] 1.2 阅读 `design.md`，确认不实现 `CompositeBodyEntity`。
- [x] 1.3 运行 `openspec validate refactor-connected-body-action-subject --strict --no-interactive`。

## 2. Current Behavior Audit
- [x] 2.1 审计 `ActionSpec` 默认 move 行为是否仍通过统一 action request 入口。
- [x] 2.2 审计普通 push 是否以 handoff result 结束 source action。
- [x] 2.3 审计 `BodyResolver` 多成员结果是否只来自 port connected group。
- [x] 2.4 审计 `ConflictResolver` 多 member commit 是否只服务 connected body subject。
- [x] 2.5 标记任何命名或测试仍把普通 push 描述为 parent retry 的位置。

## 3. Subject Boundary Implementation
- [x] 3.1 定义 action subject 的单 entity 和 connected body 两种语义。
- [x] 3.2 确认普通 move 默认 subject 是 single entity，除非由 port graph 解析为 connected body view。
- [x] 3.3 确认普通 push 命中目标时生成 handoff，不把目标加入 source 的 subject。
- [x] 3.4 确认 connected body view 使用稳定 root 和扁平 members。
- [x] 3.5 确认 root 只是代表，不是父节点。

## 4. Port Composition Boundary
- [x] 4.1 确认 `PortConnectorComponent` 只表达连接能力。
- [x] 4.2 确认 port 连接不表达控制权、等待关系或父子层级。
- [x] 4.3 确认 `PortConnectionSystem` 读取 final `PortConnectorComponent`。
- [x] 4.4 为未来 `PortGraphCache` / `ConnectedBodyCache` 保留 dirty-driven 优化边界，但不在本变更实现。

## 5. Automated Tests
- [x] 5.1 新增或更新 Unity TestFramework EditMode：普通 push source handoff 后保持原位。
- [x] 5.2 新增或更新 Unity TestFramework EditMode：handoff target 独立裁决成功。
- [x] 5.3 新增或更新 Unity TestFramework EditMode：handoff target 阻塞失败不回写 source 成功移动。
- [x] 5.4 新增或更新 Unity TestFramework EditMode：port connected body 作为一个 subject 移动所有 members。
- [x] 5.5 新增或更新 Unity TestFramework EditMode：connected body 任一 member 被阻塞则所有 members 保持原位。
- [x] 5.6 新增或更新 Unity TestFramework EditMode：connected body part 默认不能绕过 subject 单独普通 move。
- [x] 5.7 更新 `AuthoritativeMoveVerification` 覆盖普通 push 与 connected body subject 的分层结果。

## 6. Validation
- [x] 6.1 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 6.2 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 6.3 运行 Unity TestFramework EditMode。
- [x] 6.4 记录仍需用户手动 Play Mode / 双客户端验证的端到端项目。
