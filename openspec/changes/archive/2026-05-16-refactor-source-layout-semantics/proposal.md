# Change: 重构源码目录语义与文件划分

## Why
当前 Shared GameCore、Fantasy Hotfix 和 Unity ClientWorld 已经形成服务端权威、共享规则、客户端镜像的架构边界，但源码目录和部分文件粒度仍混合了配置、规则、运行时外壳、调试工具和展示职责，导致查找成本高，也容易在后续重构中把服务端权威规则、客户端显示和测试辅助路径混在一起。

## What Changes
- 明确 Shared GameCore 的目录语义，把领域模型、配置导入、规则管线、世界存储、空间索引、快照同步、runtime effect 和测试辅助分成稳定边界。
- 明确 Fantasy Hotfix 的目录语义，把 Handler、应用服务、权威 tick、同步、调试编辑、世界装配和协议转换分开，Handler 不承载规则或存储细节。
- 明确 Unity ClientWorld 的目录语义，把 Bootstrap、Networking、Mirror、Presentation、Input、DebugTools、EditorTools 和 Tests 分开，客户端默认路径只镜像服务端结果。
- 迁移文件时保持命名空间、asmdef/csproj、Unity `.meta` 引用、Fantasy source generator 规则和现有行为语义稳定。
- 为目录约束增加 Unity TestFramework EditMode 覆盖，并保留服务端验证与手动端到端验证路径。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `authoritative-move-runner`, `client-world-runner`
- Affected code: `Shared/DG.GameCore`, `Server/Hotfix/AuthoritativeMove`, `Server/Tests/AuthoritativeMoveVerification`, `Client/DG_Client/Assets/Scripts/ClientWorld`, `Client/DG_Client/Assets/Tests/Editor/ClientWorld`
- 非目标：本变更不改变 action policy、仲裁规则、Arch storage 语义、协议字段、正式 Luban 配置格式或客户端预测能力。
