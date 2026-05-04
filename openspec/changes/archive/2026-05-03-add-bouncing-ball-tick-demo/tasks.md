## 1. 协议与范围确认
- [x] 1.1 读取 `Tools/NetworkProtocol/Outer/OuterMessage.proto`，确认现有 JoinWorld、observer 和 move notify 字段。
- [x] 1.2 决定第一版复用 `G2C_EntityMovedNotify` 还是新增 demo snapshot/delta 协议。
- [x] 1.3 如果需要新增协议，先在 `OuterMessage.proto` 增加 demo Join/Observe、snapshot 或 delta 消息。
- [x] 1.4 确认协议字段包含 entity id、配置标识、server tick、位置和边界范围最小数据。
- [x] 1.5 运行协议导出工具。
- [x] 1.6 检查服务端生成目录出现新增消息和 opcode。
- [x] 1.7 检查客户端生成目录出现新增消息、opcode 和 helper。
- [x] 1.8 确认没有手动修改任何生成的 `.g.cs` 或生成目录文件。

## 2. 服务端 Demo 配置与 World 数据模型
- [x] 2.1 新增或扩展服务端 demo world 状态容器。
- [x] 2.2 定义 demo 配置结构：entity instance、配置标识、初始组件、tag。
- [x] 2.3 定义运动体配置的最小组件：位置、速度方向、可反弹 tag。
- [x] 2.4 定义边界体配置的最小组件：边界范围、边界 tag。
- [x] 2.5 初始化 demo world 时按配置创建第一个 entity。
- [x] 2.6 初始化 demo world 时按配置创建第二个 entity。
- [x] 2.7 提供读取运动体 snapshot 的查询方法。
- [x] 2.8 提供读取边界体 snapshot 的查询方法。
- [x] 2.9 保证服务端模型不依赖 UnityEngine 或 MonoBehaviour。
- [x] 2.10 确认规则查询不依赖 entity 名字。

## 3. 服务端 Tick 与规则 System
- [x] 3.1 新增 demo tick runner 或等价生命周期入口。
- [x] 3.2 设置固定 tick interval。
- [x] 3.3 实现启动 tick loop。
- [x] 3.4 实现重复启动保护。
- [x] 3.5 实现关闭或释放时停止 tick loop。
- [x] 3.6 新增基于组件/tag 查询的运动体普通移动规则。
- [x] 3.7 新增 X 轴边界反弹规则。
- [x] 3.8 新增 Y 轴边界反弹规则。
- [x] 3.9 每次 tick 后递增 server tick。
- [x] 3.10 每次状态变化后记录 dirty state。

## 4. 服务端同步 System
- [x] 4.1 新增 demo observer 集合或复用现有 observer 集合并隔离 demo 语义。
- [x] 4.2 在客户端 Join/Observe demo 时注册 observer。
- [x] 4.3 Join/Observe 成功后发送运动体 snapshot。
- [x] 4.4 Join/Observe 成功后发送边界体 snapshot。
- [x] 4.5 tick dirty flush 时构造运动体状态广播。
- [x] 4.6 广播内容使用服务端 world 最终状态。
- [x] 4.7 observer 为空时跳过发送但不停止 tick。
- [x] 4.8 广播后清理已 flush 的 dirty state。
- [x] 4.9 日志记录 server tick、entity id、位置、observer 数量。

## 5. Fantasy Handler 边界
- [x] 5.1 新增或扩展 demo Join/Observe Handler。
- [x] 5.2 Handler 使用 `FTask`。
- [x] 5.3 Handler 只注册 session 和触发 snapshot，不计算球运动。
- [x] 5.4 Handler 对失败情况返回错误码或可验证 reason。
- [x] 5.5 Handler 类保持 Fantasy 约定的 sealed class 和 file-scoped namespace。
- [x] 5.6 确认 Handler 不直接修改运动体 entity 的位置或速度。

## 6. Unity 客户端接收与 World 应用
- [x] 6.1 新增或扩展 demo 网络接入组件。
- [x] 6.2 连接可用后发送 Join/Observe demo 请求。
- [x] 6.3 收到运动体 snapshot 后在当前 `World` 创建或更新 entity。
- [x] 6.4 收到边界体 snapshot 后在当前 `World` 创建或更新 entity。
- [x] 6.5 收到运动体 delta 后应用服务端最终位置。
- [x] 6.6 应用服务端状态时进入现有 dirty flush 链路。
- [x] 6.7 客户端不在本地执行反弹权威规则。
- [x] 6.8 缺失当前 `ClientWorldRunner` 时输出可验证日志。

## 7. Unity 演示场景与可视化
- [x] 7.1 新增或复用一个最小 demo scene。
- [x] 7.2 场景包含 FantasyRuntime 或现有连接入口。
- [x] 7.3 场景包含 `ClientWorldRunner` 和当前 `World` bootstrap。
- [x] 7.4 根据配置标识绘制或实例化运动体视图。
- [x] 7.5 根据配置标识绘制或实例化边界体视图。
- [x] 7.6 显示或记录当前 server tick、entity id 和球坐标。
- [x] 7.7 验证两个客户端不会创建第二个独立 world。

## 8. 自动化测试
- [x] 8.1 新增服务端规则验证：普通移动。
- [x] 8.2 新增服务端规则验证：X 轴反弹。
- [x] 8.3 新增服务端规则验证：Y 轴反弹。
- [x] 8.4 新增服务端规则验证：dirty flush。
- [x] 8.5 新增服务端规则验证：重复启动保护。
- [x] 8.6 新增 Unity TestFramework 测试：运动体 snapshot 创建 entity。
- [x] 8.7 新增 Unity TestFramework 测试：边界体 snapshot 创建 entity。
- [x] 8.8 新增 Unity TestFramework 测试：应用运动体 delta 后坐标更新。
- [x] 8.9 新增 Unity TestFramework 测试：重复 server tick 或重复 snapshot 保持幂等。
- [ ] 8.10 运行 Unity TestFramework 并记录通过结果。

## 9. 手动端到端验收
- [ ] 9.1 启动服务端。
- [ ] 9.2 确认服务端日志出现 demo tick 启动。
- [ ] 9.3 启动 Unity 客户端 A。
- [ ] 9.4 启动 Unity 客户端 B。
- [ ] 9.5 确认 A 和 B 都 Join/Observe demo 成功。
- [ ] 9.6 确认 A 和 B 都收到运动体 snapshot。
- [ ] 9.7 确认 A 和 B 都收到边界体 snapshot。
- [ ] 9.8 确认服务端持续广播运动体 tick 状态。
- [ ] 9.9 确认 A 显示的 server tick、entity id、坐标与服务端日志一致。
- [ ] 9.10 确认 B 显示的 server tick、entity id、坐标与服务端日志一致。
- [ ] 9.11 确认球在两个客户端中持续反弹。
- [ ] 9.12 停止服务端，确认客户端不会继续生成新的权威坐标。

## 10. 文档与收口
- [x] 10.1 在实现说明中记录 Handler/System/Dirty/Sync 的实际文件路径。
- [x] 10.2 记录自动化测试命令和结果。
- [ ] 10.3 记录双客户端手动验收步骤和观察结果。
- [x] 10.4 运行 `openspec validate add-bouncing-ball-tick-demo --strict --no-interactive`。
- [ ] 10.5 所有任务完成后再把本文件对应任务勾选为完成。
