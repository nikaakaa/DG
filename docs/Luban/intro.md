# 介绍 | Luban

**Luban** 是一个强大、易用、优雅、稳定的游戏配置解决方案。它设计目标为满足从小型到超大型游戏项目的简单到复杂的游戏配置工作流需求。

luban可以处理丰富的文件类型，支持主流的语言，可以生成多种导出格式，支持丰富的数据检验功能，具有良好的跨平台能力，并且生成极快。 luban有清晰优雅的生成管线设计，支持良好的模块化和插件化，方便开发者进行二次开发。开发者很容易就能将luban适配到自己的配置格式，定制出满足项目要求的强大的配置工具。

luban标准化了游戏配置开发工作流，可以极大提升策划和程序的工作效率。

---

## 核心特性
1. 丰富的源数据格式。支持excel族(csv,xls,xlsx,xlsm)、json、xml、yaml、lua等
2. 丰富的导出格式。 支持生成binary、json、bson、xml、lua、yaml等格式数据
3. 增强的excel格式。可以简洁地配置出像简单列表、子结构、结构列表，以及任意复杂的深层次的嵌套结构
4. 完备的类型系统。不仅能表达常见的规范行列表，由于**支持OOP类型继承**，能灵活优雅表达行为树、技能、剧情、副本之类复杂GamePlay数据
5. 支持多种的语言。支持生成c#、java、go、cpp、lua、python、typescript 等语言代码
6. 支持主流的消息方案。 protobuf(schema + binary + json)、flatbuffers(schema + json)、msgpack(binary)
7. 强大的数据校验能力。ref引用检查、path资源路径、range范围检查等等
8. 完善的本地化支持
9. 支持所有主流的游戏引擎和平台。支持Unity、Unreal、Cocos、Godot、Laya、微信小游戏等
10. 良好的跨平台能力。能在Win,Linux,Mac平台良好运行。
11. 支持所有主流的热更新方案。hybridclr、ilruntime、{x,t,s}lua、puerts等
12. 清晰优雅的生成管线，很容易在luban基础上进行二次开发，定制出适合自己项目风格的配置工具。

---

## 代码使用预览

### C# 使用示例
```csharp
// 一行代码可以加载所有配置。 cfg.Tables 包含所有表的一个实例字段。
var tables = new cfg.Tables(file => return new ByteBuf(File.ReadAllBytes($"{gameConfDir}/{file}.bytes")));
// 访问一个单键表
Console.WriteLine(tables.TbItem.Get(12).Name);
// 支持 operator []用法
Console.WriteLine(tables.TbMail[1001].Desc);
```

### typescript 使用示例
```typescript
let tables = new cfg.Tables(f => JsHelpers.LoadFromFile(gameConfDir, f))
console.log(tables.TbItem.get(12).Name)
```

### go 使用示例
```go
if tables , err := cfg.NewTables(loader) ; err != nil { println(err.Error()) return}
println(tables.TbItem.Get(12).Name)
```

---

## license

Luban is licensed under the [MIT](https://github.com/focus-creative-games/luban/blob/main/LICENSE) license
