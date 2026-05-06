---
name: dg-component-system-workflow
description: 在 D:\Unity_Project_1\DG 中新增、检查或修复 component-system / world rule 工作流时使用。适用于 Luban 配置实体能力、同步 ComponentKind、注册 ComponentApplicationRegistry、实现 Shared GameCore system、维护 Shared/DG.GameCore/Rules 下的服务端权威 action/intent/plan/commit 主路、补 Unity EditMode 测试与服务端验证、确认 ClientWorld/ClientMapWorld 仍是 Shared GameWorld 的薄适配层。
---

# DG Component-System Workflow

## 先读

先读 `references/component-system-workflow.md`。它是当前 DG component-system 与 world rule 工作流的主说明。

如果用户正在走 OpenSpec 提案、实现或归档流程，同时读 `openspec/AGENTS.md`，严格遵守当前阶段边界。归档后的 `refactor-rule-system-hardcoding` 已把服务端权威规则主路收口到 state-driven action / intent / plan / commit 管线；当前规则目录结构以 `Shared/DG.GameCore/Rules` 为主入口。

## 核心判断

- `Config/Luban/Defines/gamecore.xml` 是 `ComponentKind` 的配置来源。
- `Config/Luban/Run.ps1` 是 Luban 导出入口，会调用 `Tools/sync_luban_component_kind.py`。
- `Shared/DG.GameCore/Config/ComponentKind.cs`、Luban 生成的 `cfg.gamecore.ComponentKind`、XML 中的 name/value 必须一致。
- `Config/Luban/Datas/gamecore/entity_archetype.xlsx` 是正式实体组件组合入口。
- `FallbackGameConfigProvider` 和 `DefaultWorldConfig` 只用于测试、编辑器兜底或最小 demo 兜底，不能反向成为正式配置源。
- `Tools/NetworkProtocol/Outer/OuterMessage.proto` 是客户端到服务端协议的唯一来源；不要手写修复 generated protocol。
- component 是纯数据，放在 `Shared/DG.GameCore/Components/Components.cs`。
- `ComponentApplicationRegistry` 负责 `ComponentKind -> component` 构造注册，目前是有意保留的第一版代码注册表。
- `EntityBuilder` 只负责创建 entity、复制 tags、遍历 component kind、委托 component 应用。
- `GameWorld` 只做 entity 存储、component store、空间索引、dirty、snapshot/delta。
- 规则入口位于 `Shared/DG.GameCore/Rules`，主路走 `WorldAction -> BehaviorIntent -> IntentArbiter -> RulePlanner -> ConflictResolver -> Commit`。
- `BehaviorIntentDefinitions` 是 intent 默认 source tag、ability tag、required tags、blocked tags、cancel policy 的集中入口，未知 intent kind 必须显式失败。
- `Shared/DG.GameCore/Movement` 不再是规则管线主入口；旧 `MovementResolveSystem` / legacy movement 引用应当视为回归风险。
- Unity 客户端运行时代码位于 `Client/DG_Client/Assets/Scripts/ClientWorld`；`ClientMapWorld` 只做 Unity 到 Shared `GameWorld` 的镜像/适配，不写规则裁决。

## 执行顺序

1. 先查当前状态：
   - `openspec list`
   - `openspec list --specs`
   - `rg -n "ComponentKind|ComponentApplicationRegistry|EntityBuilder|FallbackGameConfigProvider|ClientMapWorld|StateDrivenRuleExecutionSystem|BehaviorIntentDefinitions" Shared/DG.GameCore/Rules Shared/DG.GameCore Client/DG_Client/Assets/Scripts/ClientWorld Server Config Tools`
2. 按 Luban 配置实体能力。
3. 在 Shared GameCore 增加 component 和注册逻辑。
4. 若能力会触发移动、推动、占用、body、pending continuation 或冲突语义，接入 state-driven rule 主路。
5. 只在显示层处理 Unity 视觉映射，不在 `ClientMapWorld` 写规则。
6. 补 Unity TestFramework EditMode 测试和服务端验证。
7. 跑验证命令，分清 OpenSpec、Shared build、server verification、Unity EditMode、Play Mode 手测的边界。

## 常见错误

- 新增 Shared 脚本后 Unity csproj 找不到类型：刷新 Unity scripts/assets，再确认生成的项目文件包含新文件。
- 配置了 component 但实体没有对应 component：依次检查 XML、Excel、生成 JSON、Luban enum、runtime enum、`ComponentApplicationRegistry`。
- 客户端重复写规则：检查 `ClientMapWorld` 和 runtime system，把裁决移回 Shared/server state-driven rule system。
- fallback 和 Luban 不一致：默认把 fallback 当成过期副本，除非用户明确要改测试/demo 兜底；正式数据以 Luban 为准。
- generated protocol 有类型但 `.proto` 没有：这是数据源漂移，先补 `.proto` 并重新导出，不继续依赖当前 generated。
- `MovementResolveSystem` 或旧 `Shared/DG.GameCore/Movement` 路径出现在服务端权威主路：视为回归；服务端 tick runner 应走 `StateDrivenRuleExecutionSystem`。
- `openspec validate --strict --no-interactive` 没有目标时可能返回 Nothing to validate；全量校验使用 `openspec validate --all --strict --no-interactive`。

## 验证

常用命令：

```powershell
openspec validate <change-id> --strict --no-interactive
openspec validate --all --strict --no-interactive
dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore
dotnet .\Fantasy.ProtocolExportTool.dll export --silent
dotnet build Server/Hotfix/Hotfix.csproj --no-restore -v minimal
dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore
```

Unity 侧使用 Unity TestFramework 跑 EditMode tests，不要 build Unity Player。
