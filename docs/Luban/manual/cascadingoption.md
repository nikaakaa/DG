# 层级参数机制

Luban的大多数内置模板都使用了层级参数(Cascading Option)机制，即逐级缩减模块名，直到查找到选项为止。

## 参数名规则

参数名支持命名空间，跟csharp代码的命名空间类似。以`a.b.c.key`参数为例，它的命名空间为`a.b.c`，基本名为`key`。

## 层级搜索规则

查找参数`{m1}.{m2}...{mk}.{n1}`，会先查找完整参数，如果找不到，再依次删减最下一层命名空间，直至找到为止。以`a.b.c.key`为例，按照以下顺序查找选项值：
1. `a.b.c.key`
2. `a.b.key`
3. `a.key`
4. `key`

## 层级搜索规则的意义

如果只有一个公共的`outputCodeDir`和`outputDataDir`，生成将会相互覆盖。层级参数较好地解决了这个问题。

以code target为例，如果只有一个目标，简单使用`-x outputCodeDir=xxx`即可。如果有多个目标，如需要同时生成cs-bin和java-bin，则只需`-x cs-bin.outputCodeDir=cs_path`和`-x java-bin.outputCodeDir=java_path`，即可分别为他们分别指定参数。
