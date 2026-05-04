## 1. 删除 delta 数据模型
- [ ] 1.1 在共享 WorldDelta 模型中增加 removed entity id 集合。
- [ ] 1.2 调整 `GameWorld.RemoveEntity` 后的 dirty/flush 行为，使删除实体进入 removed 集合。
- [ ] 1.3 保持已有 changed entity delta 行为不变。
- [ ] 1.4 增加服务端验证用例：删除实体后 `FlushDelta` 包含 removed id。
- [ ] 1.5 增加服务端验证用例：删除不存在实体不产生 removed id。

## 2. 删除协议广播
- [ ] 2.1 修改 Outer 协议源，让 `G2C_WorldDeltaNotify` 包含 `RemovedEntityIds`。
- [ ] 2.2 运行协议导出工具并确认服务端生成代码更新。
- [ ] 2.3 确认客户端生成代码和 helper 更新。
- [ ] 2.4 更新服务端 WorldDelta notify 构造逻辑，填充 removed id。
- [ ] 2.5 更新客户端 delta handler，先删除 removed id，再应用 changed entity。
- [ ] 2.6 增加 Unity EditMode 测试：客户端收到 removed id 后移除镜像实体。

## 3. 服务端调试编辑权限
- [ ] 3.1 增加最小调试编辑开关读取入口。
- [ ] 3.2 让调试编辑 Handler 在开关关闭时统一拒绝请求。
- [ ] 3.3 增加服务端验证用例：关闭开关时建造不修改世界。
- [ ] 3.4 增加服务端验证用例：关闭开关时拖拽不修改世界。
- [ ] 3.5 增加服务端验证用例：关闭开关时删除不修改世界。

## 4. 服务端调试编辑协议
- [ ] 4.1 定义 `C2G_DebugSpawnEntityRequest` 和响应。
- [ ] 4.2 定义 `C2G_DebugMoveEntityRequest` 和响应。
- [ ] 4.3 定义 `C2G_DebugRemoveEntityRequest` 和响应。
- [ ] 4.4 运行协议导出工具并确认服务端生成代码更新。
- [ ] 4.5 确认客户端生成代码和 helper 更新。

## 5. 服务端调试编辑 Handler
- [ ] 5.1 实现建造 Handler，通过 `EntitySpawnSpec` 添加实体到权威 `GameWorld`。
- [ ] 5.2 实现拖拽 Handler，通过调试传送更新实体坐标。
- [ ] 5.3 实现删除 Handler，从权威 `GameWorld` 移除实体。
- [ ] 5.4 每个成功 Handler 在回复后触发统一 WorldDelta 广播。
- [ ] 5.5 每个失败 Handler 返回明确 reason，且不广播。
- [ ] 5.6 增加服务端验证用例：建造成功产生 changed entity delta。
- [ ] 5.7 增加服务端验证用例：拖拽成功产生 changed entity delta。
- [ ] 5.8 增加服务端验证用例：删除成功产生 removed id delta。

## 6. Unity 调试编辑工具
- [ ] 6.1 增加 Play Mode 可用的 Runtime 调试 UI 入口。
- [ ] 6.2 增加底部快捷栏容器。
- [ ] 6.3 增加快捷栏槽位：阻挡体、球、传送带、拖拽、删除。
- [ ] 6.4 增加数字键切换当前槽位。
- [ ] 6.5 增加当前槽位高亮显示。
- [ ] 6.6 增加鼠标指向格子的高亮框。
- [ ] 6.7 实现鼠标到格子坐标的拾取。
- [ ] 6.8 增加建造 ghost 预览。
- [ ] 6.9 增加 `R` 键旋转当前建造方向。
- [ ] 6.10 在 ghost 和快捷栏中显示当前建造方向。
- [ ] 6.11 实现建造请求提交，不直接本地创建权威实体。
- [ ] 6.12 实现拖拽选择已有实体。
- [ ] 6.13 实现拖拽 ghost 或选中态跟随目标格。
- [ ] 6.14 实现拖拽请求提交，不走普通移动规则。
- [ ] 6.15 实现删除模式选中实体反馈。
- [ ] 6.16 实现删除请求提交，不直接本地删除权威实体。
- [ ] 6.17 在调试面板显示最近一次请求结果和失败 reason。
- [ ] 6.18 确认调试 UI 不包含正式玩家背包、资源消耗或建造冷却逻辑。

## 7. Unity 测试与手动验证
- [ ] 7.1 增加 Unity EditMode 测试：拾取格子坐标稳定。
- [ ] 7.2 增加 Unity EditMode 测试：数字键切换快捷栏槽位。
- [ ] 7.3 增加 Unity EditMode 测试：`R` 键旋转建造方向。
- [ ] 7.4 增加 Unity EditMode 测试：调试工具在无 session 时不提交请求。
- [ ] 7.5 增加 Unity EditMode 测试：delta 删除先于 changed entity 应用。
- [ ] 7.6 运行 Unity TestFramework EditMode 测试并记录结果。
- [ ] 7.7 手动启动服务端和两个客户端。
- [ ] 7.8 在客户端 A 选择阻挡体槽位并建造，确认客户端 B 同步显示。
- [ ] 7.9 在客户端 A 选择传送带槽位、旋转方向并建造，确认客户端 B 同步显示方向。
- [ ] 7.10 在客户端 A 拖拽球或传送带，确认客户端 B 同步坐标。
- [ ] 7.11 在客户端 A 删除一个调试实体，确认客户端 B 同步消失。
- [ ] 7.12 关闭调试开关后重复建造/拖拽/删除，确认服务端拒绝且客户端不变化。

## 8. 最终验证
- [ ] 8.1 运行 `openspec validate add-authoritative-debug-world-editor --strict --no-interactive`。
- [ ] 8.2 运行服务端权威移动验证项目。
- [ ] 8.3 运行服务端 Hotfix 构建验证。
- [ ] 8.4 运行客户端脚本构建验证，但不构建 Unity Player。
- [ ] 8.5 确认所有任务完成后更新 checklist。
