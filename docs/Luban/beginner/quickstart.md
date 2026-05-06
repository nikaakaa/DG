# 快速上手

## 安装

1. 安装[dotnet sdk 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)或更高版本sdk
2. 下载[luban_examples项目](https://gitee.com/focus-creative-games/luban_examples)。该项目包含测试配置与大量示例，后续默认指此项目文件。

提示：`luban_examples/Tools/Luban`目录下的Luban可能不是最新版，可从[release](https://github.com/focus-creative-games/luban/releases)下载最新版或自行编译源码。

## 准备配置工程

直接使用luban_examples项目中的MiniTemplate，后续操作在此基础上修改。

## 创建Reward表

在`MiniTemplate/Datas`目录下创建`reward.xlsx`文件，内容格式如下：
- 第1行是字段名行，单元格A1必须以##开头，表名后接字段名（如`##reward id,item_id,count`）。
- 第2行是字段类型行，第一个单元格必须为`##type`，后接对应类型（如`##type int,int,int`）。
- 第3行是分组行，`c`表示字段属于客户端，`s`表示属于服务器，`c,s`或留空表示同时属于所有。
- 第4行是注释行，以##开头，可0-N个且位置任意。
- 第5行起是数据行（如`1,101,5;2,102,10;3,103,15`）。

在Datas目录下的`__tables__.xlsx`添加reward表声明（如`reward,reward.xlsx`）。

## 生成配置数据

直接运行`MiniTemplate/gen.bat`（Win平台）或`MiniTemplate/gen.sh`(MacOS/Linux平台)，成功后会显示`bye~`，并在`MiniTemplate/output`目录下生成json配置数据，可打开查看。
