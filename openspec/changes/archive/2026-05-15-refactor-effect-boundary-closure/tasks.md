## 1. Proposal Validation
- [x] 1.1 审阅 `runtime-component-results`、`client-world-runner`、`authoritative-move-runner`、`data-driven-runtime-actions` 现有规格�?- [x] 1.2 确认本变更覆�?`add-effect-application-layer` 的剩余边界问题，不再新建拆分 proposal�?- [x] 1.3 运行 `openspec validate refactor-effect-boundary-closure --strict --no-interactive`�?
## 2. Shared GameCore Write Boundary
- [x] 2.1 梳理 `GameWorld.AddRuntimeEffect`、`RemoveRuntimeEffect`、`AddRuntimeEffectSource`、`RemoveRuntimeEffectSource` 当前调用点�?- [x] 2.2 收紧普通调用方可见�?runtime effect mutation API�?- [x] 2.3 提供 commit-only 测试 helper，替代测试直接调�?public runtime effect 写入口�?- [x] 2.4 增加源码扫描测试，禁止规则层和普通测试绕�?commit �?runtime effect�?
## 3. Source Contribution Model
- [x] 3.1 定义 component/tag contribution �?source add/remove 语义�?- [x] 3.2 �?`SetComponentResult` 只写�?runtime component source，不覆盖 final component�?- [x] 3.3 �?`AddTag` 写入 tag source contribution�?- [x] 3.4 �?`RemoveTag` 移除指定 tag source contribution，不直接修改 final tag�?- [x] 3.5 �?resolver �?static source、runtime effect source、runtime tag source 统一结算 final Component/tag�?- [x] 3.6 覆盖 static tag + runtime tag add/remove �?static tag 保留�?
## 4. Runtime Reset Boundary
- [x] 4.1 增加�?entity 移除 runtime effect/source �?commit �?service 能力�?- [x] 4.2 确认 reset runtime state 不修�?Position、Direction、PlayerControl 或实体生命周期�?- [x] 4.3 覆盖同一 entity �?effect 乱序添加、乱序删除、reset 后回�?static final result�?- [x] 4.4 覆盖 stack replace/refresh/allow multiple/reject duplicate 下的 reset 行为�?
## 5. Server Debug Effect Path
- [x] 5.1 �?debug apply runtime effect �?`RuntimeEffectKind` 解析到明�?`EffectSpecId`�?- [x] 5.2 �?debug path �?Luban-backed provider 或显�?debug effect registry 获取 `EffectSpec`�?- [x] 5.3 移除 debug 主路径中临时 `RuntimeEffectKind -> EffectSpec` 构造�?- [x] 5.4 覆盖 debug apply/remove 仍走 commit，并广播 final Component/tag delta�?
## 6. Unity Client Authority Boundary
- [x] 6.1 移除或隔�?`ClientMoveNetworkSubmitter` 的本�?runtime effect apply/remove fallback�?- [x] 6.2 确认 server-authoritative debug effect 按钮只提�?RPC，不本地修改 authoritative mirror�?- [x] 6.3 如果保留 offline/local sandbox effect，重命名并隔�?wiring�?- [x] 6.4 增加 EditMode 测试证明 server-authoritative mode 不调用本�?effect resolver/store mutation�?
## 7. Automated Validation
- [x] 7.1 `openspec validate refactor-effect-boundary-closure --strict --no-interactive`
- [x] 7.2 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- [x] 7.3 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- [x] 7.4 Unity TestFramework EditMode：effect commit-only、tag source、client authority boundary、runtime reset�?
## 8. Manual End-To-End Verification
- [x] 8.1 用户�?Play Mode 连接服务端权威路径�?- [x] 8.2 用户启动两个客户端，确认 observer 收到一�?WorldDelta�?- [x] 8.3 用户通过调试工具施加 temporary pushable / immobile / auto move / port / tag effect�?- [x] 8.4 用户乱序移除 effect，并确认两个客户�?final Component/tag 一致�?- [x] 8.5 用户清空对象 runtime effects，确认对象回到静态配置决定的 final state�?