# Luban 沙盒测试台工作流

## 目标

沙盒测试台用于持续验证新增 Luban entity、component、tag 和规则组合。它读取当前 `IGameConfigProvider` 的 `EntityArchetype`，测试 JSON 只保存摆放、操作和预期，不保存 component 组合，也不覆盖正式 Luban Excel。

## 使用方式

1. 在 Unity 中打开 `Client/DG_Client`。
2. 使用菜单 `DG/ClientWorld/Create Client ClientMapWorld Runner Demo Scene` 重新生成 Demo 场景。
3. 进入 Play。
4. 左侧从 `Luban Entity` 列表选择实体。
5. 在右侧输入坐标和方向，或直接点击格子生成。
6. 点击已有实体选中，拖拽到目标格可移动。
7. 使用 tag 下拉和 `+ Tag` / `- Tag` 验证 `WorldTag` 对规则的影响。
8. `Server` 勾选时走 Debug RPC 和服务端权威 `WorldDelta`；取消勾选时走本地 Shared runner。
9. `Clipboard` 可从剪贴板导入 AI 生成的 JSON 草案。
10. `Save` / `Load` 保存或读取 JSON 用例。

## JSON 草案格式

```json
{
  "schemaVersion": 1,
  "name": "immune conveyor push",
  "steps": [
    { "kind": "spawn", "alias": "belt", "configId": 2001, "x": 0, "y": 0, "direction": "Right" },
    { "kind": "spawn", "alias": "box", "configId": 1003, "x": 0, "y": 0 },
    { "kind": "setTag", "alias": "box", "tag": "ImmuneMechanismPush", "enabled": true },
    { "kind": "tick", "ticks": 1 },
    { "kind": "expectPosition", "alias": "box", "x": 0, "y": 0 }
  ],
  "expectations": [
    { "kind": "expectLastResult", "success": false, "reason": "blocked by tag" }
  ]
}
```

## 边界

- AI 草案只能引用 Luban `configId`、坐标、方向、alias、`WorldTag` 和预期。
- 测试 JSON 不能包含 `components`、`tags`、`archetype` 或 `archetypeId`。
- `ClientMapWorld` 只负责镜像 Shared `GameWorld`，不要在里面加入规则裁决。

## 验证

- OpenSpec: `openspec validate add-luban-driven-sandbox-test-lab --strict --no-interactive`
- Shared: `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
- Client 编译检查: `dotnet build Client/DG_Client/Assembly-CSharp.csproj --no-restore /m:1 /p:IntermediateOutputPath=D:\Unity_Project_1\DG\.build\ClientAsmObj2\ /p:OutputPath=D:\Unity_Project_1\DG\.build\ClientAsmBin2\`
- Unity TestFramework: 运行 EditMode `SandboxScenarioTests`
- 服务端验证: `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
