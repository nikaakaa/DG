## 1. 规格和兼容边界
- [ ] 1.1 确认 `PresentationEvent` 不参与权威状态计算。
- [ ] 1.2 确认 `PresentationEvent` 随 `WorldDelta` 发送，不新增独立 Notify。
- [ ] 1.3 确认 `kindId` 和 `cueId` 使用字符串 ID，不新增表现枚举。
- [ ] 1.4 确认 `payloadJson` 只承载表现专属参数。
- [ ] 1.5 确认本变更不实现客户端预测、回滚、状态历史或输入重放。

## 2. Shared GameCore 数据模型
- [ ] 2.1 新增 `PresentationEvent` 值对象。
- [ ] 2.2 新增 `PresentationEventKindId` 或等价强类型字符串值对象。
- [ ] 2.3 新增 `PresentationCueId` 或等价强类型字符串值对象。
- [ ] 2.4 将 `DirtyWorldJournal` 扩展为保存表现事件。
- [ ] 2.5 将 `WorldDelta` 扩展为携带 `PresentationEvents`。
- [ ] 2.6 保留兼容入口，避免一次性破坏现有动画测试。
- [ ] 2.7 确保 `WorldDelta` 的实体快照和删除列表仍是唯一权威状态来源。

## 3. 配置边界
- [ ] 3.1 在 Luban 定义中新增 action 到 presentation 的配置结构。
- [ ] 3.2 配置字段至少包含 action spec id、kind id、cue id 和可选 payload 模板。
- [ ] 3.3 导出配置数据，不手改生成文件。
- [ ] 3.4 在配置 provider 或 registry 中解析表现配置。
- [ ] 3.5 删除或隔离 tick runner 中普通 action 名称到 style key 的硬编码映射。

## 4. 服务端表现事件编排
- [ ] 4.1 新增 `PresentationEventComposer`。
- [ ] 4.2 composer 输入为 server tick、ready actions、rule result 和必要上下文。
- [ ] 4.3 player move 生成 `entity.moved` 事件。
- [ ] 4.4 mechanism push 生成 `entity.pushed` 事件。
- [ ] 4.5 spawn/remove 生成对应表现事件。
- [ ] 4.6 rotate pivot 成功生成 pivot 表现事件，pivot 参数进入 payload。
- [ ] 4.7 rotate pivot blocked/bounce 生成 bounce 和 impact 表现事件。
- [ ] 4.8 metadata-only 表现事件在没有 entity snapshot 变化时仍可通过 delta 广播。
- [ ] 4.9 `AuthoritativeWorldTickRunner` 只调用 composer，不再内联表现推断。

## 5. 协议和同步
- [ ] 5.1 修改 Outer 协议源，给 `G2C_WorldDeltaNotify` 增加 `PresentationEvents`。
- [ ] 5.2 导出服务端和 Unity 客户端协议代码。
- [ ] 5.3 `AuthoritativeWorldSyncSystem` 将 `WorldDelta.PresentationEvents` 写入 notify。
- [ ] 5.4 客户端网络运行时将协议事件转换为客户端表现事件。
- [ ] 5.5 兼容期保留旧 `AnimationMetadata` 读取或完成一次性迁移。
- [ ] 5.6 不手改 `Server/Entity/Generate` 或 `Client/DG_Client/Assets/Scripts/Generate` 下生成文件。

## 6. Unity 客户端表现层
- [ ] 6.1 新增或改造 `ClientPresentationLayer` 消费 `PresentationEvent`。
- [ ] 6.2 世界状态应用仍只读取 entity snapshots 和 removed ids。
- [ ] 6.3 表现事件按 `serverTick/eventId` 幂等入队。
- [ ] 6.4 客户端使用 `cueId` 查找本地表现配置。
- [ ] 6.5 旋转、bounce、impact 从 `payloadJson` 读取专属参数。
- [ ] 6.6 `ClientWorldVisuals` 只根据表现事件播放，不根据 action 名称裁决规则。
- [ ] 6.7 缺失或未知 `kindId/cueId` 时不破坏世界状态应用。

## 7. 自动测试
- [ ] 7.1 Unity TestFramework EditMode：`PresentationEvent` 不改变 `ClientMapWorld` 权威状态。
- [ ] 7.2 Unity TestFramework EditMode：收到 move 表现事件后生成客户端表现队列事件。
- [ ] 7.3 Unity TestFramework EditMode：收到未知 `kindId` 时状态仍正常应用。
- [ ] 7.4 Unity TestFramework EditMode：rotate pivot payload 能生成 pivot 表现。
- [ ] 7.5 Unity TestFramework EditMode：metadata-only 表现事件没有 snapshot 时仍可入队。
- [ ] 7.6 Server verification：player move 生成稳定 `entity.moved`。
- [ ] 7.7 Server verification：mechanism push 生成稳定 `entity.pushed`。
- [ ] 7.8 Server verification：rotate pivot success/blocked 生成包含 payload 的表现事件。
- [ ] 7.9 Server verification：普通 action 新增表现不需要在 tick runner 中新增 action-name 分支。

## 8. 构建和验证
- [ ] 8.1 运行 `openspec validate refactor-presentation-events --strict --no-interactive`。
- [ ] 8.2 运行 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- [ ] 8.3 运行 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- [ ] 8.4 运行相关 Unity TestFramework EditMode 测试。

## 9. 手动端到端验证
- [ ] 9.1 用户启动 Fantasy 服务端和 Unity Play Mode。
- [ ] 9.2 用户启动两个 Unity 客户端并加入同一权威世界。
- [ ] 9.3 用户验证玩家移动后两个客户端世界状态一致，且播放 `entity.moved` cue。
- [ ] 9.4 用户验证机关推动后两个客户端世界状态一致，且播放 `entity.pushed` cue。
- [ ] 9.5 用户验证 rotate pivot 成功和 blocked/bounce 表现一致。
- [ ] 9.6 用户验证禁用或未知表现 cue 不影响权威坐标收敛。
