# 加载配置

## 安装Luban.Runtime
加载数据依赖Luban Runtime代码。Unity+C#可通过Package Manager安装`com.code-philosophy.luban`包，地址为`https://gitee.com/focus-creative-games/luban_unity.git`或`https://github.com/focus-creative-games/luban_unity.git`；其他语言可在[示例项目](https://gitee.com/focus-creative-games/luban_examples/tree/main/Projects)中复制对应项目的Luban Runtime代码。

## unity + c# + json
完成Luban.Runtime安装后，使用以下代码加载配置：
```csharp
void Load() {
    var tables = new cfg.Tables(Loader);
    Console.WriteLine(tables.TbGlobal.Name);
    Console.WriteLine(tables.TbItem.Get(12).Name);
    Console.WriteLine(tables.TbMail[1001].Desc);
}

private static JSONNode LoadJson(string file) {
    return JSON.Parse(File.ReadAllText($"{your_json_dir}/{file}.json", System.Text.Encoding.UTF8));
}
```

## unity项目中使用c#代码并自动判断加载bin或json配置
通过反射创建cfg.Tables实例，可在开发期使用JSON格式、发布时使用Bin格式，实现代码不变自动适配：
```csharp
void Start() {
    var tablesCtor = typeof(cfg.Tables).GetConstructors()[0];
    var loaderReturnType = tablesCtor.GetParameters()[0].ParameterType.GetGenericArguments()[1];

    System.Delegate loader = loaderReturnType == typeof(ByteBuf)
        ? new System.Func<string, ByteBuf>(LoadByteBuf)
        : (System.Delegate)new System.Func<string, JSONNode>(LoadJson);

    var tables = (cfg.Tables)tablesCtor.Invoke(new object[] { loader });
    Console.WriteLine(tables.TbGlobal.Name);
    Console.WriteLine(tables.TbItem.Get(12).Name);
    Console.WriteLine(tables.TbMail[1001].Desc);
}

private static JSONNode LoadJson(string file) {
    return JSON.Parse(File.ReadAllText($"{your_json_dir}/{file}.json", System.Text.Encoding.UTF8));
}

private static ByteBuf LoadByteBuf(string file) {
    return new ByteBuf(File.ReadAllBytes($"{your_json_dir}/{file}.bytes"));
}
```

## 其他项目类型
请在[Projects](https://gitee.com/focus-creative-games/luban_examples/tree/main/Projects)中找到与你项目类型相符的示例项目，参考其加载代码即可。
