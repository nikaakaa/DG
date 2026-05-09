## 1. Proposal Gate
- [x] 1.1 审阅 `proposal.md`，确认第一阶段只做 Component 结果层边界。
- [x] 1.2 审阅 `design.md`，确认没有恢复旧 runtime effect / ability 半成品。
- [x] 1.3 审阅 spec delta，确认 `ComponentStateResolver` 是唯一合成边界。
- [x] 1.4 确认本阶段不引入 Ability 系统。
- [x] 1.5 获得批准后再进入实现阶段。

## 2. Runtime Source Model
- [x] 2.1 定义第一阶段支持的 runtime effect kind。
- [x] 2.2 定义 `RuntimeEffectSpec`。
- [x] 2.3 定义 `RuntimeEffectInstance`。
- [x] 2.4 定义 runtime source id。
- [x] 2.5 定义 runtime expire tick。
- [x] 2.6 实现 `RuntimeEffectStore` 的 add。
- [x] 2.7 实现 `RuntimeEffectStore` 的 remove。
- [x] 2.8 实现 `RuntimeEffectStore` 的 expire。
- [x] 2.9 定义 effect result contribution。
- [x] 2.10 定义 EffectLifecycleSystem 的 active / removed / expired 输出。
- [x] 2.11 确认 `RuntimeEffectStore` 不引用 `GameWorld`。
- [x] 2.12 确认 `RuntimeEffectStore` 不暴露 component 写入 API。

## 3. Static Source Model
- [x] 3.1 列出 first slice 中由 archetype 提供的 static component source。
- [x] 3.2 为 `BlockingComponent` 建立 static source 读取路径。
- [x] 3.3 为 `AutoMoveComponent` 建立 static source 读取路径。
- [x] 3.4 为 `PushableComponent` 建立 static source 读取路径。
- [x] 3.5 为 `PortConnectorComponent` 建立 static source 读取路径。
- [x] 3.6 为移动权限类结果建立 static source 读取路径。
- [x] 3.7 确认 `PositionComponent` 不进入 static/runtime resolver。
- [x] 3.8 确认 `DirectionComponent` 不进入 static/runtime resolver。
- [x] 3.9 确认 `PlayerControlComponent` 不进入 static/runtime resolver。

## 4. ComponentStateResolver
- [x] 4.1 定义 resolver 输入。
- [x] 4.2 定义 resolver 输出。
- [x] 4.3 定义 entity + component kind + source 的合成 key。
- [x] 4.4 实现 static source 和 runtime source 同 Component 合并。
- [x] 4.5 实现 runtime source remove 后保留 static result。
- [x] 4.6 实现多个 runtime source 的引用保留。
- [x] 4.7 实现最后一个 source 消失后移除 final component。
- [x] 4.8 实现 final result 变化后标记 dirty。
- [x] 4.9 实现 dynamic port 的 union merge。
- [x] 4.10 实现 runtime port 移除后保留 static port。
- [x] 4.11 实现移动权限类结果的 source merge。
- [x] 4.12 确认 resolver 不处理移动、推动、冲突或网络同步。

## 5. GameWorld Integration
- [x] 5.1 将 first slice static component 应用改为进入 resolver source。
- [x] 5.2 保留非 first slice component 的现有创建路径。
- [x] 5.3 让 resolver 应用 final `BlockingComponent` 到 `GameWorld`。
- [x] 5.4 让 resolver 应用 final `AutoMoveComponent` 到 `GameWorld`。
- [x] 5.5 让 resolver 应用 final `PushableComponent` 到 `GameWorld`。
- [x] 5.6 让 resolver 应用 final `PortConnectorComponent` 到 `GameWorld`。
- [x] 5.7 让 resolver 应用 final movement permission result 到 `GameWorld`。
- [x] 5.8 确认 `GameWorld` 仍只保存 final component result。
- [x] 5.9 确认 `GameWorld` 不保存 Ability / Effect 语义。

## 6. Rules Boundary
- [x] 6.1 确认 auto move 规则只读取 final `AutoMoveComponent`。
- [x] 6.2 确认 blocking 规则只读取 final `BlockingComponent`。
- [x] 6.3 确认 push 规则只读取 final `PushableComponent`。
- [x] 6.4 确认 `PortConnectionSystem` 只读取 final `PortConnectorComponent`。
- [x] 6.5 确认可移动 / 不可移动由 final component result + Rules 仲裁。
- [x] 6.6 确认 Rules 不查询 `RuntimeEffectStore`。
- [x] 6.7 确认 Rules 不查询 `AbilityKind`。
- [x] 6.8 确认 Rules 不查询 `EffectKind`。
- [x] 6.9 保留 `WorldTag` 现有 intent 仲裁，不新增新的 tag 依赖。

## 7. Snapshot / Delta / Client Mirror
- [x] 7.1 审查 `EntitySnapshot` 当前 final component 字段。
- [x] 7.2 为 first slice 缺失的 final component result 增加最小同步字段。
- [x] 7.3 更新 `WorldDelta` changed entity 表达。
- [x] 7.4 更新 `Tools/NetworkProtocol/Outer/OuterMessage.proto`。
- [x] 7.5 重新导出 Fantasy 协议生成物。
- [x] 7.6 更新服务端 snapshot/delta notify 映射。
- [x] 7.7 更新客户端 snapshot/delta handler 映射。
- [x] 7.8 确认 `ClientMapWorld` 只应用服务端 final component result。
- [x] 7.9 确认 `ClientMapWorld` 不解析 runtime effect。

## 8. No Ability Boundary
- [x] 8.1 确认本阶段不新增 Ability 模块。
- [x] 8.2 确认不存在 `AbilityKind -> ComponentKind` 直接映射。
- [x] 8.3 确认没有用 Ability grant/remove 写 `GameWorld` component。
- [x] 8.4 确认 dynamic port 不通过 Ability 实现。
- [x] 8.5 确认可移动 / 不可移动仲裁不通过 Ability 实现。

## 9. Unity TestFramework EditMode Tests
- [x] 9.1 新增 static + runtime 同 Component 测试。
- [x] 9.2 新增 runtime remove 不误删 static result 测试。
- [x] 9.3 新增 runtime 多来源 remove A 保留 B 测试。
- [x] 9.4 新增 runtime 最后来源移除后 final component 消失测试。
- [x] 9.5 新增 `RuntimeEffectStore` 不直接写 Component 测试。
- [x] 9.6 新增 dynamic port 合成测试。
- [x] 9.7 新增 runtime port 移除不误删 static port 测试。
- [x] 9.8 新增 movement permission 仲裁测试。
- [x] 9.9 新增不引入 Ability / 不存在 `AbilityKind -> ComponentKind` 测试。
- [x] 9.10 新增 Rules 不查询 Effect / Ability 测试。
- [x] 9.11 新增 `ClientMapWorld` 只镜像 final component result 测试。
- [x] 9.12 在 Unity Test Runner 中运行相关 EditMode tests。

## 10. Manual End-to-End Verification
- [ ] 10.1 启动服务端。
- [ ] 10.2 打开 Unity Play Mode 客户端 A。
- [ ] 10.3 打开 Unity Play Mode 客户端 B。
- [ ] 10.4 让两个客户端都 Join 同一个服务端世界。
- [ ] 10.5 A 施加临时 AutoMove 类 runtime effect。
- [ ] 10.6 B 通过服务端 WorldDelta 看到最终 AutoMove component result。
- [ ] 10.7 移除 A 施加的 runtime effect。
- [ ] 10.8 确认纯 runtime AutoMove result 消失。
- [ ] 10.9 对静态已有 Blocking 的 entity 施加并移除 runtime Blocking。
- [ ] 10.10 确认静态 Blocking result 仍存在。
- [ ] 10.11 对静态已有 port 的 entity 施加并移除 runtime port。
- [ ] 10.12 确认静态 port result 仍存在。
- [ ] 10.13 施加 immobile/rooted 类 runtime effect。
- [ ] 10.14 确认移动请求由服务端 Rules 拒绝。
- [ ] 10.15 确认客户端没有本地 effect 生效判断日志。
- [x] 10.16 不执行 Unity Player build。

## 11. OpenSpec Validation
- [x] 11.1 运行 `openspec validate refactor-static-runtime-component-results --strict --no-interactive`。
- [x] 11.2 如有错误，修正 proposal/spec/tasks 格式。
- [x] 11.3 重新运行 strict validation，直到通过。
