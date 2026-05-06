# 集成到项目

## 安装 dotnet SDK

按快速上手要求，安装[dotnet sdk 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)。

## 下载luban_examples项目

使用git clone或者下载zip的方式下载luban_examples项目。

## 编译Luban（可选）

一般来说`luban_examples/Tools/Luban`目录已包含最新的Luban二进制代码。若要自己编译：

* 将[Luban](https://gitee.com/focus-creative-games/luban)克隆到luban_examples同级目录（目录名必须为luban）
* 运行 `luban_examples/Tools/build-luban.bat`

成功后，`luban_examples/Tools/Luban`会替换为最新版本的二进制代码。

## 复制Luban工具到你的项目

大多数项目有专用目录放第三方工具，如`{proj}/Tools`，将`luban_examples/Tools/Luban`复制到任意合适目录即可。

## 创建策划配置目录

将`luban_examples/MiniTemplate`复制到项目合适位置，如`{proj}`，建议重命名为DataTables。

## 修改luban.conf

打开`{DataTables}/gen.bat`，替换`LUBAN_DLL`路径为实际Luban.dll的目录。运行`{DataTables}/gen.bat`确保能正常执行。

## 新增客户端和服务器的gen.bat脚本

### 客户端脚本（gen_client.bat）
```bat
set GEN_CLIENT={Luban.dll的路径}
set CONF_ROOT={DataTables目录的路径}
dotnet %GEN_CLIENT% ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir={生成的代码的路径} ^
    -x outputDataDir={生成的数据的路径}
```

### 服务器脚本（gen_server.bat）
```bat
set GEN_CLIENT={Luban.dll的路径}
set CONF_ROOT={DataTables目录的路径}
dotnet %GEN_CLIENT% ^
    -t server ^
    -c cs-dotnet-json ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputCodeDir={生成的代码的路径} ^
    -x outputDataDir={生成的数据的路径}
```

## 运行时加载配置

请看下一节运行时加载配置。
