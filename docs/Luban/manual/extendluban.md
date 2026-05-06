# 扩展Luban实现

## 创建扩展模块

源码中除了`Luban.Core`和`Luban`以外的项目都是扩展项目，开发者可参考它们创建Luban扩展模块。SimpleLauncher会自动搜索模块名含Luban的模块，因此**扩展模块名最好包含Luban**，否则需使用`SimpleLauncher.ScanResigerAssembly`注册自定义扩展类。

以创建Luban.Demo模块为例，步骤如下：
- 创建项目Luban.Demo
- 在Luban项目中引用Luban.Demo
- Luban.Demo项目引用Luban.Core
- 从Luban.CSharp项目复制AssemblyInfo.cs到本目录

## 可扩展的部分

- Pipeline
- Schema Collector
- Data Loader
- CodeTarget
- DataTarget
- DataValidator
- CodeStyle
- PostProcessor
- OutputSaver
- TextProvider

## 将Luban嵌入到其他C#工程中

有时需在其他工具中嵌入Luban而非直接用命令行工具。嵌入操作：
- 引用Luban.Core，建议也引入核心默认实现的Luban.XXX.Builtin项目
- 使用SimpleLauncher类初始化环境
- 使用DefaultPipeline或自定义Pipeline运行生成管线
