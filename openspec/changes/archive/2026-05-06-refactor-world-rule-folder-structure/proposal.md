# Change: 整理规则层与客户端世界目录结构

## Why
当前 `Shared/DG.GameCore/Movement` 已经承载 action、intent、仲裁、规划、冲突提交、pending state 和连接体规则，职责早已超过“移动”。Unity 客户端的 `Assets/Scripts/Map` 也已经承载世界镜像、网络同步、输入、调试工具和显示层，继续叫 `Map` 会让后续开发者误以为它只是地图数据结构。

## What Changes
- 将 Shared 规则管线目录从 `Shared/DG.GameCore/Movement` 收口为 `Shared/DG.GameCore/Rules`，让目录表达“世界规则执行层”而不是单一移动玩法。
- 将规则管线内部按 `Actions`、`Intents`、`Arbitration`、`Planning`、`Commit`、`Pending`、`Connectivity`、`Primitives`、`Systems` 拆分，避免 `BehaviorArbitration.cs` 继续承载过多职责。
- 删除空的 legacy 目录边界，避免已迁移的 `MovementResolveSystem` 路径继续作为概念入口出现。
- 将 Unity 客户端 `Assets/Scripts/Map` 改为 `Assets/Scripts/ClientWorld`，表达它是服务端权威 world 的客户端镜像、网络和显示模块。
- 第一阶段保持行为不变，不引入客户端预测、离线移动裁决、新规则或新配置表。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `client-world-runner`
- Affected code:
  - `Shared/DG.GameCore/Movement/**`
  - `Shared/DG.GameCore/Rules/**`
  - `Client/DG_Client/Assets/Scripts/Map/**`
  - `Client/DG_Client/Assets/Scripts/ClientWorld/**`
  - `Client/DG_Client/Assets/Tests/Editor/Map/**`
  - Unity `.meta` files for moved scripts and folders
