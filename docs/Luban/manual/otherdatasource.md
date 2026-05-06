# 非excel数据源

Luban支持多种非Excel数据源，包括json、lua、xml、yaml和独有的lite格式，以下是详细介绍：

## 演示所用的类型

以`DemoType2`为例展示各格式数据填写方式，其包含bool、int、float、string、bean、enum、array、list、map等字段。

## 数据目录

若`table.inputFiles`指向目录，会遍历目录树，忽略`.~_`开头的文件，剩余文件作为数据文件。excel文件仍默认读取多个记录。

## 单记录格式

### json格式
- set类型：`[v1,v2,...]`
- map类型：`[[k1,v1],[k2,v2]...]`（支持任意类型key）
- 多态bean：需`$type`属性指定类型名
示例：
```json
{
  "x1":true,
  "x14":{"$type":"DemoD2","x1":1,"x2":2},
  "k8":[[2,2],[4,10]]
}
```

### lua格式
- 以`return`开头
- map类型：`{[key1]=value1,[key2]=value2}`
- 多态bean：需`_type_`属性
示例：
```lua
return {
  x1 = false,
  x14 = { _type_="DemoD2", x1=1, x2=3 },
  k8 = {[2]=10,[3]=12}
}
```

### xml格式
- 多态bean：需`type`属性
示例：
```xml
<data>
  <x1>true</x1>
  <x14 type="DemoD2">
    <x1>1</x1>
    <x2>2</x2>
  </x14>
  <k8>
    <item><key>2</key><value>10</value></item>
  </k8>
</data>
```

### yaml格式
- 与json类似，set和map格式相同
- 多态bean：需`$type`属性
- 支持`.yaml`和`.yml`后缀
示例：
```yaml
x1: true
x14:
  $type: DemoD2
  x1: 1
  x2: 2
k8:
- [2,2]
- [4,10]
```

## lite格式

Luban独有的无字段名格式，后缀为`.lit`，更简洁。

### 基础类型格式
| 类型     | 格式                                  | 说明                     |
|----------|---------------------------------------|--------------------------|
| bool     | true/false/1/0                        | 大小写无关               |
| 数值类型 | 直接填写数字                          | byte/short/int/long/float/double |
| string   | abc/'abc'/\"abc\"                     | 自动剔除前后空白         |
| datetime | 1970-01-01 00:00:00                   |                          |
| 容器     | {1,2,3}                               | array/list/set           |
| map      | {{1,2},{3,4}}                         |                          |
| bean     | {1,2,3}                               | 按字段顺序填写           |
| 多态bean | {DemoD2,1,2}                          | 第一个字段为类型名       |

示例：
```
{1122,false,2,128,112233445566,1.3,1122,yf,{1},D,{DemoD2,1,3},1970-01-01 00:00:00,{1,2},{{2,10},{3,12}}}
```

## 复合文件格式

支持从单个文件读取多个记录或指定字段：
- `*@file.json`：读取文件中所有记录
- `*field@file.json`：读取文件中`field`字段的记录列表
- `field@file.json`：读取文件中`field`字段的单个记录

示例配置：
```xml
<table name="TbCompositeJsonTable1" value="CompositeJsonTable1" input="*table1@composite_tables.json,*@composite_tables2.json,one_record.json"/>
<table name="TbCompositeJsonTable3" value="CompositeJsonTable3" mode="one" input="table3@composite_tables.json"/>
```
