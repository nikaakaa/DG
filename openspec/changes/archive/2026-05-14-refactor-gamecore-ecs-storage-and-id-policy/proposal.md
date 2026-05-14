# Change: GameCore ECS 存储与行为 ID 化

## Why

DG 的目标规模已经从 demo 的几十个实体推进到每房间几千 active entities。当前 `GameWorld` 在语义上是 ECS，但底层仍是 `Dictionary<long, GameEntity>`、`Dictionary<Type, IComponentStore>` 和 `Dictionary<long, TComponent>`，热路径仍存在全表扫、排序拷贝、字符串 spec/tag/policy 句柄流入运行时的问题。

同时，行为层已经在推进数据驱动策略，但 `ActionSpecId`、`BlockedResultPolicyId`、配置 tag、表现 style 等仍有字符串来源。配置层可读字符串可以保留，底层行为层和未来 ECS 存储层需要收敛到稳定数值/强类型 ID，避免规则热路径继续依赖字符串比较、字符串 hash 或 action 名字语义。

## What Changes

- 定义 GameCore 存储演进的 proposal 级目标：先建立可观测、可回滚的 ECS 存储抽象和查询边界，再分阶段替换内部字典存储。
- 定义第一阶段只做 runtime contract 收口：外部语义保持 `GameWorld` API 不变，内部增加 entity location、component set、typed query / subset index 的边界。
- 定义行为层 ID 化目标：配置层可以保留可读字符串别名，但进入 Shared GameCore 运行时规则、action queue、arbitration、planning、commit、deferred output 后必须使用强类型或数值 ID。
- 定义字符串边界：字符串只允许出现在 Luban 源数据、导入解析、错误信息、调试展示、表现映射或兼容测试入口；核心规则不得通过字符串 spec id、policy id、entity tag 或玩法名选择策略。
- 明确空间 chunk/cell 和 ECS archetype chunk 是两个概念：空间索引保留，ECS 存储只替换组件迭代和 entity location。
- 加入 Unity TestFramework、Shared/server 验证、压测/观测任务和手动端到端验证说明。

## Impact

- Affected specs:
  - `shared-gamecore-entity-rules`
  - `data-driven-runtime-actions`
  - `map-runtime-foundation`
- Affected code:
  - `Shared/DG.GameCore/World/GameWorld.cs`
  - `Shared/DG.GameCore/Components/ComponentStore.cs`
  - `Shared/DG.GameCore/Entities/GameEntity.cs`
  - `Shared/DG.GameCore/Spatial/SpatialEntityIndex.cs`
  - `Shared/DG.GameCore/Rules/Actions/ActionSpecs.cs`
  - `Shared/DG.GameCore/Rules/Actions/WorldActions.cs`
  - `Shared/DG.GameCore/Rules/Execution/StateDrivenRules.cs`
  - `Shared/DG.GameCore/Rules/Planning/*`
  - `Shared/DG.GameCore/Rules/Commit/*`
  - `Shared/DG.GameCore/Config/LubanActionSpecRegistry.cs`
  - `Config/Luban/Defines/gamecore.xml`
  - `Config/Luban/Datas/gamecore/*`
  - `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`
  - Unity EditMode tests under `Client/DG_Client/Assets/Tests/Editor`

## Clarifications

- 本提案不直接实现 Unity DOTS，也不把 Shared GameCore 改成依赖 Unity.Collections、Burst、Jobs 或 UnityEngine。
- 本提案不要求配置表作者立刻放弃可读字符串。可读字段可以作为导入别名，但运行时规则层必须使用解析后的 ID。
- 本提案不在 proposal 阶段写实现代码。实施前需要用户批准。
