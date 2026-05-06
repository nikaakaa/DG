# 命令行工具

## 命令格式

```
dotnet <path_of_luban.dll> [args]
  -s, --schemaCollector        schema collector name
  --conf                       Required. luban conf file
  -t, --target                 Required. target name
  -c, --codeTarget             code target name
  -d, --dataTarget             data target name
  -p, --pipeline               pipeline name
  -f, --forceLoadTableDatas    force load table datas
  -i, --includeTag             include tag
  -e, --excludeTag             exclude tag
  --variant                    field variants
  -o, --outputTable            output table
  --timeZone                   time zone
  --customTemplateDir          custom template dirs
  -x, --xargs                  args like -x a=1 -x b=2
```

## Code Target

|code target|描述|
|---|---|
|cs-bin|C#，读取bin格式文件|
|cs-simple-json|C#，使用SimpleJSON读取json文件，推荐Unity客户端|
|cs-dotnet-json|C#，使用System.Text.Json读取json文件，推荐dotnet core服务器|
|cs-newtonsoft-json|C#，使用Newtonsoft.Json读取json文件|
|cs-editor-json|C#，读取与保存记录为单个json文件|
|java-bin|java，读取bin格式文件|
|java-json|java，使用gson读取json格式文件|
|go-bin|go，读取bin格式文件|
|go-json|go，读取json格式文件|
|cpp-sharedptr-bin|cpp，智能指针，读取bin格式文件|
|python-json|python，读取json格式文件|
|typescript-bin|typescript，读取bin格式文件|
|typescript-json|typescript，读取json格式文件|
|lua-lua|lua，读取lua格式文件|
|lua-bin|lua，读取bin格式文件|
|protobuf2/3|生成protobuf schema文件|
|flatbuffers|生成flatbuffers schema文件|

## Data Target

|data target|描述|
|---|---|
|bin|Luban独有的binary格式，紧凑高效，推荐正式发布|
|json|json格式，map输出成[[key, value]]格式|
|json2|与json类似，但map输出成{"key":"value"}格式|
|lua|lua格式|
|xml|xml格式|
|yaml|yaml格式|
|bson|bson格式|
|msgpack|msgpack的二进制格式|
|protobuf2-bin/protobuf3-bin|protobuf二进制格式|
|protobuf2-json/protobuf3-json|protobuf json格式|
|flatbuffers-json|flatbuffers json格式|

## xargs

|参数|描述|
|---|---|
|{codeTarget}.outputCodeDir|代码目标的输出目录|
|{dataTarget}.outputDataDir|数据目标的输出目录|
|codeStyle|代码命名风格|
|namingConvention.{codeTarget}.{location}|命名风格自定义|
|dataExporter|数据导出器|
|outputSaver|数据保存器(local/null)|
|l10n.provider|本地化文本Provider|
|pathValidator.rootDir|path校验器搜索根目录|
|json.compact|是否输出紧凑json|
|{code\|data}.lineEnding|行尾符(CR/LF/CRLF)|

## 示例

### unity + c# + json
```bat
dotnet %LUBAN_DLL% -t all -c cs-simple-json -d json --conf %CONF_ROOT%\luban.conf -x outputCodeDir=Assets/Gen -x outputDataDir=..\GenerateDatas\json
```

### unity + c# + bin
```bat
dotnet %LUBAN_DLL% -t all -c cs-bin -d bin --conf %CONF_ROOT%\luban.conf -x outputCodeDir=Assets/Gen -x outputDataDir=..\GenerateDatas\bytes
```
