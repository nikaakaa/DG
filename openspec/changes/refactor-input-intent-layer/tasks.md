## 1. 输入意图模型

- [x] 1.1 定义 `InputIntent` 核心数据结构。
- [x] 1.2 定义 `InputKind`，至少包含 `Move`、`Wait`，预留 `Attack`、`Interact`、`Skill`、`Cancel`。
- [x] 1.3 定义 `InputSourceKind`，至少包含 `Player`、`Debug`，预留 `AI`、`Replay`、`Script`。
- [x] 1.4 定义 `TargetHint`，支持空、方向、格子、实体 id 的提示语义。
- [x] 1.5 定义 `RhythmJudge`，第一阶段默认 `None`，不实现真实拍点判定。

## 2. 客户端输入入口

- [x] 2.1 使用 Unity Input System 建立正式玩家输入入口。
- [x] 2.2 将方向输入转换为 `Move` 类型 `InputIntent`。
- [x] 2.3 保留旧 `ClientWorldDemo` 作为迁移期 demo 或调试入口，不作为正式输入路径。
- [x] 2.4 记录 pending intent，按 `clientInputId` 追踪状态。
- [x] 2.5 确保提交 intent 不直接修改 `ClientMapWorld.CoreWorld` 权威坐标。

## 3. 服务端输入收口

- [x] 3.1 将普通玩家输入入口归一化为服务端认可的 `InputIntent`。
- [x] 3.2 实现或改造 `InputIntentBuffer`，按 actor + tick/beat + 输入通道收口。
- [x] 3.3 同 actor 同 tick/beat 多个普通 `Move` intent 只保留最后一个。
- [x] 3.4 被覆盖 intent 返回 `Replaced` 或等价输入生命周期状态。
- [x] 3.5 过期、重复或超限 intent 不进入 action pipeline。

## 3A. 服务端输入窗口修正

- [ ] 3A.1 拆分客户端声明 `BeatTick` 与服务端消费窗口 tick，避免把客户端字段直接作为普通输入缓冲 key。
- [ ] 3A.2 在服务端 handler 或输入缓冲边界分配权威 `ConsumeTick`，默认同一服务端收集窗口的快速输入归入同一 consume tick。
- [ ] 3A.3 将普通玩家输入缓冲 key 改为 actor + consume tick + channel + inputKind。
- [ ] 3A.4 确保没有明确 input-ahead 时，快速连续输入不会因为 `World.ServerTick` 前进而自动排入多个未来 tick。
- [ ] 3A.5 保留客户端 `BeatTick` / `RhythmJudge` 作为声明和诊断字段，不让它决定服务端消费窗口。

## 4. 授权边界

- [x] 4.1 建立 `IntentAuthorization` 或等价边界。
- [x] 4.2 普通 Player 来源校验 session 与 actor 控制关系。
- [x] 4.3 授权读取 `PlayerControlComponent` 和最终组件/状态事实。
- [x] 4.4 Debug、AI、Replay、Script 来源保留清晰入口和权限边界。
- [x] 4.5 授权失败只返回输入拒绝，不执行玩法规则。

## 5. Intent Adapter

- [x] 5.1 建立 `IntentAdapter` 或等价适配层。
- [x] 5.2 将已消费 `Move(direction)` intent 转为现有 player movement action。
- [x] 5.3 服务端使用权威当前位置和 direction 推导目标格。
- [x] 5.4 保持碰撞、推动、机关、冷却等裁决在现有 action pipeline 内完成。
- [x] 5.5 输入响应不替代 `WorldDelta`。

## 6. 协议和兼容

- [x] 6.1 规划 `C2G_InputIntentRequest` 或等价协议字段。
- [x] 6.2 迁移期允许旧 `C2G_PlayerInputRequest` 适配到 `InputIntent`。
- [x] 6.3 旧 Direction-only 请求不得绕过 `InputIntentBuffer` 直接进入 action queue。
- [x] 6.4 协议导出后不手改生成文件。

## 7. 自动测试

- [x] 7.1 Unity TestFramework EditMode：Input System 方向输入生成 Move intent。
- [x] 7.2 Unity TestFramework EditMode：pending intent 不改变权威镜像坐标。
- [x] 7.3 Unity TestFramework EditMode：Replaced / Expired / Rejected 清理 pending intent。
- [x] 7.4 Server verification：同 actor 同 tick Move intent 后输入覆盖前输入。
- [x] 7.5 Server verification：不同 actor intent 互不覆盖。
- [x] 7.6 Server verification：未授权 actor intent 被拒绝。
- [x] 7.7 Server verification：授权通过但撞墙等规则失败仍由 action pipeline 返回。
- [ ] 7.8 Server verification：同一 actor 快速连续输入在同一服务端消费窗口只消费最后一个 intent。
- [ ] 7.9 Server verification：客户端声明 BeatTick 不会让普通输入自动滚入多个未来服务端 tick。
- [ ] 7.10 Unity TestFramework EditMode：快速方向输入 pending 状态按服务端响应覆盖/清理，不本地推动权威镜像。

## 8. 手动端到端验证

- [ ] 8.1 用户启动服务端和一个 Unity 客户端，确认 Unity Input System 方向输入能提交并最终通过 WorldDelta 收敛。
- [ ] 8.2 用户在一个服务端输入窗口内快速按多个方向，确认服务端只结算最后方向。
- [ ] 8.3 用户启动两个 Unity 客户端，确认客户端 A 输入后 A/B 最终状态一致。
- [ ] 8.4 用户验证输入失败不会 fallback 到本地 movement rule。
- [ ] 8.5 用户确认调试编辑和 runtime effect debug 路径不被普通输入合并规则覆盖。
- [ ] 8.6 用户快速连续撞击可推物，确认普通输入层不会把同一窗口输入排入多个未来 tick 导致连续派生 push。
