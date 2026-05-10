## 1. Proposal validation
- [x] 1.1 确认本 change 只覆盖同 tick 等价 deferred 合并，不处理 push 打断、抵消、TTL、能量或强度。
- [x] 1.2 对照 `refactor-action-output-cleanup`，确认两个 active changes 不重叠修改同一 spec 语义。
- [x] 1.3 运行 `openspec validate dedupe-equivalent-deferred-push --strict --no-interactive`。

## 2. Deferred equivalence model
- [x] 2.1 为 `DeferredAction` 或 helper 增加 queue-equivalence key，字段包含 ready tick、spec id、direction、resolved subject key。
- [x] 2.2 确认 key 不包含 parent causality、created tick 或现有 dedupe key。
- [x] 2.3 覆盖 subject ids 为空时回退到 entity id 的情况。

## 3. Queue merge implementation
- [x] 3.1 在 `WorldActionQueue.EnqueueDeferred()` 内部实现统一合并入口。
- [x] 3.2 合并同一 ready tick、同 spec、同 direction、同 subject 的 deferred output。
- [x] 3.3 保留不同方向、不同 subject、不同 ready tick 的 deferred output。
- [x] 3.4 保证合并后返回值或结果能让调用方知道是新入队还是被合并。
- [x] 3.5 记录等价 deferred 的 contribution count，但不把它解释为强度、优先级或额外移动。

## 4. Logging and diagnostics
- [x] 4.1 将服务端 tick runner 的 deferred 日志改成摘要输出，并继续通过现有 `Log.Info(...)` / NLog 路径输出。
- [x] 4.2 输出 raw deferred 数、实际入队数、合并数、contribution count、样本和重复 key 摘要。
- [x] 4.3 使用代码内固定样本上限，避免在爆炸场景中逐条打印所有 action 或 touched entity。

## 5. Automated tests
- [x] 5.1 增加 Unity TestFramework EditMode：两个不同 causality 的等价 deferred 只进入队列一次。
- [x] 5.2 增加 Unity TestFramework EditMode：同 subject 不同方向 deferred 不合并。
- [x] 5.3 增加 Unity TestFramework EditMode：不同 subject 同方向 deferred 仍分别入队。
- [x] 5.4 增加 Unity TestFramework EditMode：等价 deferred 合并后 contribution count 等于合并贡献数，且不会产生额外 queued action。
- [x] 5.5 增加或更新 server verification：多分支汇合到同一 subject 时下一 tick action 数不按 parent 数膨胀。
- [x] 5.6 保留已有 multi-contact fanout 测试，证明 distinct subject fanout 未被压扁。

## 6. Validation
- [x] 6.1 运行 `openspec validate dedupe-equivalent-deferred-push --strict --no-interactive`。
- [x] 6.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [x] 6.3 运行 `dotnet build Server/Hotfix/Hotfix.csproj --no-restore`。
- [x] 6.4 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [x] 6.5 让用户手动 Play Mode 复现指数增长结构，确认日志可复制、服务器不刷屏爆炸、不同方向震荡仍可观察。
