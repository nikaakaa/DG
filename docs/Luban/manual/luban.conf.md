# luban.conf

定义了luban所需要的全局配置。

## 格式

一个典型的luban.conf的配置内容如下：

```json
{
    "groups":
    [
        {"names":["c"], "default":true},
        {"names":["s"], "default":true},
        {"names":["e"], "default":true}
    ],
    "schemaFiles":
    [
        {"fileName":"Defines", "type":""},
        {"fileName":"Datas/__tables__.xlsx", "type":"table"},
        {"fileName":"Datas/__beans__.xlsx", "type":"bean"},
        {"fileName":"Datas/__enums__.xlsx", "type":"enum"}
    ],
    "dataDir": "Datas",
    "targets":
    [
        {"name":"server", "manager":"Tables", "groups":["s"], "topModule":"cfg"},
        {"name":"client", "manager":"Tables", "groups":["c"], "topModule":"cfg"},
        {"name":"all", "manager":"Tables", "groups":["c","s","e"], "topModule":"cfg"}
    ]
}
```

## dataDir

指定数据根目录，此项目配置不可为空。

## schemaFiles

定义了需要收集的schema子定义文件，可以为多个，也可以为目录，此时会递归收集目录树下所有子文件。

|字段|类型|可空|描述|
|----|----|----|----|
|fileName|string|否|需要收集的schema子定义文件，可以为文件或目录|
|type|string|否|子定义文件类型。xml格式定义文件不需要type。excel族需要用type指定包含哪种类型的定义，有效值为空白字符串、enum、bean、table|

## groups

定义了配置内可用的分组。

|字段|类型|可空|描述|
|----|----|----|----|
|names|list,string|否|分组名，包含1-n个值|
|default|bool|否|是否为table的默认导出目标|

group的name字段可以为任意值，但不要重复出现。一般是c、s这样的简单的单字符。

group的default字段对于enum、bean及field不生效。

如果field的group为空，则默认属于所有分组。

## targets

定义了导出目标。

|字段|类型|可空|描述|
|----|----|----|----|
|name|string|否|导出目标名|
|manager|string|否|生成的管理所有导出Table的管理类的名称，一般取Tables|
|groups|list,string|否|该输出目标包含哪些分组|
|topModule|string|是|类型额外的顶层命名空间，可以为空|
