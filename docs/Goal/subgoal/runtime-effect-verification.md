# 验证与测试子目标

## 定位

本子文档负责定义运行时能力与效果系统的验证策略。

项目约束：Unity 侧只使用 Unity TestFramework EditMode，不执行 Unity Player build。端到端 Play Mode 由用户手动验证。

## 验证目标

验证必须覆盖：

```text
Luban 配置导出
AbilityKind / EffectKind 同步
provider 读取 ability/effect definition
AbilitySpec 构建和清理
RuntimeEffectSpec 生成
RuntimeEffectInstance apply/refresh/remove/expire
tag contribution source set
runtime component contribution
WorldDelta effect 同步
ClientMapWorld effect mirror
服务端权威路径
```

## Luban 验证

需要验证：

```text
AbilityKind XML 与 runtime enum 一致
EffectKind XML 与 runtime enum 一致
ability_definition 导出成功
effect_definition 导出成功
LubanGameConfigProvider 能读取 AbilityDefinition
LubanGameConfigProvider 能读取 EffectDefinition
StreamingAssets 中存在导出 json
```

## Shared / Server 验证

需要验证：

```text
entity 拥有静态 AbilitySpec
effect 可以授予临时 AbilitySpec
ApplyRuntimeEffect 创建 RuntimeEffectInstance
RefreshRuntimeEffect 刷新 expire tick
RemoveRuntimeEffect 移除实例
ExpireRuntimeEffect 按 server tick 过期
多个 effect 共享同一 WorldTag 不误删
entity 删除时清理相关 effects
effect 贡献的 tag 能影响 IntentArbiter
```

## Unity EditMode 验证

需要验证：

```text
ClientMapWorld 应用 AddedEffects
ClientMapWorld 应用 RefreshedEffects
ClientMapWorld 应用 RemovedEffects
ClientMapWorld 应用 ChangedTags
ClientMapWorld 不本地裁决 effect
Debug 面板读取 mirror state
```

## 手动端到端验证

手动验证路径：

```text
1. 运行 Luban 导出。
2. 启动服务端。
3. 启动 Unity 客户端。
4. JoinWorld。
5. 通过服务端权威 Debug 工具生成触发实体。
6. 玩家进入触发格。
7. 服务端提交 runtime effect。
8. 客户端 Debug 面板显示 effect。
9. 玩家再次移动时服务端按 tag/component 裁决。
10. effect 到期后客户端显示 removed。
11. 玩家恢复正常行为。
```

## 验证分层

报告结果时必须分层：

```text
OpenSpec validate
Luban export passed
Shared build passed
Shared/server verification passed
Unity EditMode passed
Play Mode manual passed
dual-client sync manual passed
```

不能把 build passed 等同于端到端完成。

## 回归风险

验证需要防止：

```text
Fallback provider 被当成正式数据源
ClientMapWorld 写规则
Debug UI 直接改权威状态
effect 绕过 action/commit
tag contribution 误删
WorldDelta 漏同步 removed effect
```

## 与其他子文档的关系

```text
runtime-effect-luban-config.md
  提供配置验证目标。

runtime-ability-model.md
  提供 AbilitySpec / activation 验证目标。

runtime-effect-model.md
  提供 effect 生命周期验证目标。

runtime-effect-rules-integration.md
  提供 tick/action/commit 验证目标。

runtime-effect-world-delta.md
  提供客户端镜像验证目标。
```
