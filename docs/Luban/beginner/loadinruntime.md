# 运行时加载配置

我们以最常见的 Unity + c# + json 为例。

## 安装 com.code-philosophy.luban

在Package Manager中安装`com.code-philosophy.luban`包，地址 `https://gitee.com/focus-creative-games/luban_unity.git`或`https://github.com/focus-creative-games/luban_unity.git`。

## 生成代码和配置

请根据接入Luban到项目文档接入Luban，同时准备好相应的生成脚本`gen.bat`。

运行该脚本，如果一切正常，会产生一系列日志，最终一行是 `bye~`。

> ⚠️ 危险
> Luban生成时会删除`outputCodeDir`目录下的所有其他文件，请为它提供一个单独的目录，千万不要指向`Assets/Scripts`目录，它会删除掉其他代码文件！`outputDataDir`同理。

## 加载配置

只需一行代码即可加载所有配置表。整个游戏运行期间只加载一次。

```csharp
string gameConfDir = "<outputDataDir>"; // 替换为gen.bat中outputDataDir指向的目录
var tables = new cfg.Tables(file => JSON.Parse(File.ReadAllText($"{gameConfDir}/{file}.json")));
```

> 💡 提示
> 默认生成的代码不支持异步加载，而在android之类的平台不能直接读取StreamingAssets目录，因此需要自己做一些特殊处理，比如先将所有配置数据文件加载到内存后再使用`new Tables`加载。
> 你可以通过修改代码模板来支持异步加载。

## 使用配置

`cfg.Tables` 里包含所有配置表的一个实例字段。加载完 `cfg.Tables` 后，用 `tables.<表名>` 获得那个表实例。

```csharp
cfg.demo.Reward reward = tables.TbReward.Get(1001);
Console.WriteLine("reward:{0}", reward);
```

工具会根据输出的语言，自动作相应代码风格的字段名转换，也即 `boo_bar` 会被转换为 `BooBar` 这样的名字。因此推荐配置中字段名时统一使用 `xx_yy_zz` 的风格。
