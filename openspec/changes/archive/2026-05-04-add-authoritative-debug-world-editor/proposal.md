# Change: 添加权威世界调试工具

## Why
当前 Demo 已经具备服务端权威移动、多人观察和统一 WorldDelta，但实体删除还不能通过 delta 明确同步给客户端；同时缺少一个可以在 Unity 中直接建造、拖拽和删除实体的调试工具。

为了让后续组件和地图规则可以被快速验证，需要先补齐“删除协议广播”，再提供一个只在调试开关开启时可用、由服务端裁决并广播的世界调试工具。本变更只面向开发调试，不把建造、拖拽和删除定义为正式玩家玩法。

## What Changes
- 在共享 GameCore 的 WorldDelta 语义中加入删除实体表达，避免服务端删除后客户端镜像残留。
- 扩展服务端同步协议和广播逻辑，使 `G2C_WorldDeltaNotify` 同时表达变更实体和删除实体。
- 新增服务端调试编辑 RPC：建造实体、拖拽/传送实体、删除实体。
- 新增最小调试权限开关，调试编辑请求在关闭时必须被服务端拒绝。
- 新增 Unity Demo Runtime 调试 UI，提供底部快捷栏、格子高亮、ghost 预览、方向旋转、建造、拖拽和删除，并通过服务端权威路径生效。
- 新增服务端验证、Unity EditMode 测试和手动端到端验证说明。

## Impact
- Affected specs: `shared-gamecore-entity-rules`, `authoritative-move-runner`, `client-world-runner`
- Affected code: `Shared/DG.GameCore/World/GameWorld.cs`, `Shared/DG.GameCore/World/WorldDelta.cs`, `Tools/NetworkProtocol/Outer/OuterMessage.proto`, `Server/Hotfix/AuthoritativeMove`, `Client/DG_Client/Assets/Scripts/Samples/Map/ClientWorldDemo`, `Client/DG_Client/Assets/Tests/Editor/Map`
