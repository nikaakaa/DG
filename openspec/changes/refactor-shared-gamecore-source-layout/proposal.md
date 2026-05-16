## Why
Shared GameCore 的动作规则代码已经从 movement-only 演进成了 action intake、targeting、strategy、blocking、claim、planning、commit、component result、snapshot projection 等多个边界，但源码仍主要收在 `Shared/DG.GameCore/Rules` 和少量过大的文件里。这个命名会让新行为接入时继续把业务逻辑塞回“规则中心类”，也让自动生成代码、业务策略、底层求解器混在一起。

当前已有 `refactor-action-component-extension-boundaries` 负责把策略、component applicator、fact query、commit handler、result resolver 等扩展点做成注册边界。本变更只规划 Shared 层源码结构：把扩展边界落到清晰目录，明确业务逻辑、运行时编排、生成代码和领域数据各自放在哪里。

## What Changes
- 将 Shared GameCore 的行为运行时入口从 `Rules` 收口为 `ActionRuntime`，按 pipeline 职责组织目录。
- 将业务策略实现放入明确的 `Strategies`、`Targeting`、`Blocking/Outcomes`、`Commit/Handlers` 等模块目录，核心编排只保留调度和裁决职责。
- 将生成代码统一放入 `Generated` 目录，人工源码不得依赖生成文件的物理位置来决定行为。
- 保留 Shared 层纯 C#、无 UnityEngine、无 Fantasy runtime、无协议生成类型依赖的约束。
- 明确迁移期间命名空间可以暂时不随目录改动，避免一次性破坏引用；最终目录结构先落稳，再决定是否做命名空间收口。

## Out of Scope
- 不在本提案阶段移动代码或修改实现。
- 不重新定义 action policy、targeting policy、component applicator、commit handler 的扩展语义；这些由现有能力规格和 `refactor-action-component-extension-boundaries` 继续负责。
- 不修改 `.meta` 文件；实现阶段只移动/新增 `.cs`，由 Unity 刷新生成或维护 `.meta`。
- 不改变服务端 world storage backend 的 Arch 方案。

## Impact
- 影响 Shared 层源码路径、生成器输出路径、测试引用和开发工作流文档。
- 需要更新 `ActionStrategyRegistrationGeneratorMenu` 的输出路径，但生成结果仍属于 Shared 层。
- 需要 Unity TestFramework EditMode 覆盖移动后的注册、targeting、blocking、commit、snapshot/component result 路径。
- 需要用户手动端到端验证：Unity Play Mode 或两客户端场景中确认 player move、auto move、push/bounce、spawn/remove、snapshot/delta 行为未变。
