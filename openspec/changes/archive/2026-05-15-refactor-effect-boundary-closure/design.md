## Context
当前 effect 应用层已经把 action output、effect application、commit、runtime store、resolver 串起来，但边界还不够硬：
- `GameWorld.AddRuntimeEffect` / `RemoveRuntimeEffect` 是 public，测试和客户端 fallback 能绕过 commit。
- `SetComponentResult` / `AddTag` / `RemoveTag` 已进入 commit enum，但 tag remove 仍直接改 final tag。
- 服务端 debug runtime effect 走 commit，但它从 `RuntimeEffectKind` 临时构造手写 `EffectSpec`。
- Unity client 仍有本地 runtime effect 应用/移除分支，和 server-authoritative mirror 目标冲突。

这些问题会让“runtime effect source 可逆叠加层”变得不稳定：只要有人直接写 final component/tag 或本地解析 effect，就不能保证乱序增删 effect 后回到静态初始状态。

## Goals
- 让 commit 成为 runtime effect 和 runtime component/tag source 的唯一普通写入口。
- 让 `ComponentStateResolver` 成为 first-slice final component/tag 的唯一结算边界。
- 让 tag result 和 component result 使用同一类 source/contribution 生命周期模型。
- 让 debug effect 也引用配置化 effect definition 或明确 debug registry，而不是临时反推玩法对象。
- 让 Unity client 在 server-authoritative mode 下只提交意图和镜像 server final result。
- 提供可验证的按 entity 清空 runtime effect/source 能力，用于还原对象 runtime 叠加状态。

## Non-Goals
- 不实现完整对象快照回滚。
- 不把 Position、Direction、PlayerControl、实体创建删除纳入 effect resolver。
- 不实现 Attribute/Stat 系统。
- 不引入新的 GameplayTag registry。
- 不改变玩家移动、推动、传送、实体生命周期仍走 action / claim / planning / commit 的规则。
- 不要求 Unity Player build。

## Decisions
- Decision: `RuntimeEffectStore` 的 mutation API 对普通调用方收紧为 commit/resolver 内部使用；测试通过 commit helper 或专门 test fixture 进入同一边界。
- Decision: `CommitProposalKind.SetComponentResult` 写入 named source contribution，不写 final component。
- Decision: `CommitProposalKind.AddTag` / `RemoveTag` 写入或移除 tag source contribution，不直接调用 `GameWorld.AddTag` / `RemoveTag` 作为 runtime result 主路径。
- Decision: static archetype/tag sources 和 runtime effect/tag sources 共享 resolver 的 source key 模型；移除 source 后 final result 由剩余 source 重算。
- Decision: entity-level reset 只清理 runtime effect/source，不重置 Position、Direction、实体存在性或静态 archetype。
- Decision: server debug apply effect 使用 `EffectSpecId` 主路径；为了兼容旧 RPC 的 `RuntimeEffectKind`，允许服务端 debug 层有一个明确映射到 Luban/debug effect spec id 的 adapter，但 adapter 不构造临时 `EffectSpec` 作为权威定义。
- Decision: Unity client 的本地 runtime effect 分支只允许存在于明确命名的 offline/local sandbox 组件中，且 server-authoritative submitter 不调用它。
- Decision: source scan 测试作为边界保护，用于发现规则层、客户端权威路径和 debug 主路径再次读取/构造 effect 运行时状态。

## Risks / Trade-offs
- Risk: 收紧 `GameWorld` public API 会影响现有测试。
  Mitigation: 提供 commit-based test helper，并迁移测试断言到与生产一致的路径。
- Risk: tag source 化后，旧的 static tag 捕获和 runtime tag remove 可能互相覆盖。
  Mitigation: 使用 source key 区分 static、runtime effect、debug source；测试覆盖 static tag + runtime tag add/remove 乱序。
- Risk: 旧 debug RPC 仍以 `RuntimeEffectKind` 为输入。
  Mitigation: 在 server debug 边界把 kind 解析成 effect spec id；后续协议可升级为直接传 effect spec id，但本变更不强制协议破坏。
- Risk: 客户端离线调试依赖本地 effect。
  Mitigation: 如果保留离线路径，必须移动到明确 local-only 名称和 wiring，且 server-authoritative mode 测试证明不会调用。

## Migration Plan
1. 先引入 commit-only helper 和 source contribution 模型，迁移测试入口。
2. 收紧 `GameWorld` effect mutation API，并修复 Shared/server 测试。
3. 把 tag add/remove 改为 source contribution，补 static + runtime tag 回归测试。
4. 改 server debug effect 解析为 effect spec id / registry 主路径。
5. 移除或隔离 Unity client local runtime effect 分支。
6. 增加 source scan 和手动双客户端验证清单。
