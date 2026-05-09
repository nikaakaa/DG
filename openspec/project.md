# Project Context

## Purpose
DG 是一个 Unity 客户端 + Fantasy 服务端 + Shared GameCore 的服务端权威 2D 格子沙盒原型。当前目标是先稳定实体配置、组件驱动规则、权威 tick/action、WorldDelta 同步和可手动验证的沙盒测试工具，再继续扩展正式玩法。

## Tech Stack
- Unity 2022.3.62f2c1 客户端，项目根目录：`Client/DG_Client`
- Fantasy / Fantasy.Unity 2026.0.1018 网络层
- Shared C# GameCore，`netstandard2.1`
- Server / Hotfix，`.NET 8`
- Luban v4.7.0 配置导出
- Unity TestFramework EditMode 测试

## Project Conventions

### Code Style
- 生成或修改代码尽量不要写注释。
- 新增任务拆得细，方便逐条验证。
- Shared GameCore 保持纯 C#，不依赖 UnityEngine。
- Unity 客户端显示层可以读取状态，但不得复制服务端规则裁决。

### Architecture Patterns
- Luban `EntityArchetype` 是正式实体组合配置来源。
- `ComponentKind -> ComponentApplicationRegistry -> GameWorld.SetComponent` 是实体出生时应用组件的主路径。
- component-system 是配置层和实体能力层，不是完整技能系统。
- 会改变坐标、占用、tag、component state 或 world state 的运行时行为必须进入 action / intent / tick / commit 边界。
- 行为必须数据驱动：`ActionSpecId` 只用于查找配置，规则层不得根据 action 名字硬编码行为；primitive、source、target rule、blocked policy、conflict policy、subject policy、commit rule 等行为策略必须来自 `ActionSpec` 字段。
- `WorldTag` 只表达来源、能力、状态、免疫、阻挡等规则输入或过滤条件，不得替代 `ActionSpec` 策略字段；不得通过 tag 组合在规则层反向推断一套隐藏行为。
- `ClientMapWorld` 只做 Shared `GameWorld` 的 Unity 镜像适配。
- 服务端权威 `GameWorld` 是多人同步和规则裁决的真相源。

### Testing Strategy
- OpenSpec 变更必须先运行 `openspec validate <change-id> --strict --no-interactive`。
- Shared 层变更优先跑 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`。
- 服务端规则变更跑 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`。
- Unity 侧使用 Unity TestFramework EditMode。
- 不执行 Unity Player build。
- “端到端完成”必须由用户手动验证 Play Mode、服务端连接和双客户端同步。

### Git Workflow
当前工作区可能包含大量未提交和未跟踪文件。修改时只碰当前任务相关文件，不回滚用户或其他任务产生的改动。

## Domain Context
- 一个 world coord 等于一个格子，也等于 Unity 中 1 unit。
- 一个 chunk 是 32x32 cell。
- `WorldTag` 用于表达来源、能力、状态、免疫、阻挡语义。
- `ActionSpec -> ActionRequest -> ActionArbiter -> RulePlanner -> MovePlan -> ConflictResolver -> Commit` 是当前移动/推动类行为的目标运行时管线。
- 沙盒测试台用于验证 Luban 实体和临时组件组合，不生产正式 entity archetype，也不替代技能系统。

## Important Constraints
- 不要尝试 Unity Player build。
- 不要让 Unity UI 或 `ClientMapWorld` 直接裁决服务端权威规则。
- 不要把沙盒 JSON 当成第二套正式配置源。
- 不要把 component-system 扩成完整技能系统；技能/效果应作为单独 runtime pipeline 规划。
- 不要用 action 名字、entity 名字或 tag 组合硬编码普通行为分支；新增普通行为应优先扩展 `ActionSpec` 数据字段或显式新增底层 policy。
- OpenSpec proposal 阶段不得写实现代码。

## External Dependencies
- Fantasy / Fantasy.Unity
- Luban
- Newtonsoft.Json
- Unity TestFramework
