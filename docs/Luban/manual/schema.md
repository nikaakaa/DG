# schema 逻辑结构

Luban的核心为完备的类型系统，而DPP管线则是强大的扩展能力的基础。新版本的定义是独立的，不再与具体的定义格式绑定。

## 自定义类型

### enum

| 字段 | 类型 | 可空 | 描述 |
| --- | --- | --- | --- |
| namespace | string | 是 | 命名空间 |
| name | string | 否 | 类型名 |
| isFlags | bool | 是 | 是否为标志位类型 |
| isUniqueItemId | bool | 否 | 枚举值是否唯一 |
| comment | string | 是 | 注释 |
| tags | map,string,string | 是 | 自定义tag对 |
| groups | list,string | 是 | 导出分组 |
| items | list,EnumItem | 是 | 枚举项列表 |
| typeMappers | list,TypeMapper | 是 | 外部类型映射相关配置 |

EnumItem定义：

| 字段 | 类型 | 可空 | 描述 |
| --- | --- | --- | --- |
| name | string | 否 | 枚举项名 |
| alias | string | 是 | 别名 |
| value | string | 是 | 枚举值 |
| comment | string | 是 | 注释 |
| tags | map,string,string | 是 | 自定义tag对 |

value如果为空，则自动从上一个枚举项值开始递增，如果是第一个枚举值，则值取0。value可以为10进制整数或者0x10之类的16进制整数。也可以是其他枚举值的或组合，如`A|B`。

### bean

用于定义复合结构，对应于C#里的class或struct。

| 字段 | 类型 | 可空 | 描述 |
| --- | --- | --- | --- |
| namespace | string | 是 | 命名空间 |
| name | string | 否 | 类型名 |
| parent | string | 是 | 父类名 |
| isValueType | bool | 是 | 是否为值类型 |
| comment | string | 是 | 注释 |
| tags | map,string,string | 是 | 自定义tag对 |
| alias | string | 是 | 别名 |
| sep | string | 是 | 默认字段分割符 |
| groups | list,string | 是 | 导出分组 |
| fields | list,Field | 是 | 字段列表 |
| typeMappers | list,TypeMapper | 是 | 外部类型映射相关配置 |

**bean支持继承和多态**。如果parent字段为非空，则表示继承该父类的字段。如果parent不包含命名空间，会从bean当前命名空间内查找该类型，否则全局查找。所有被继承的bean都是抽象类，不可实例化。

Field定义：

| 字段 | 类型 | 可空 | 描述 |
| --- | --- | --- | --- |
| name | string | 否 | 字段名 |
| alias | string | 是 | 字段别名 |
| type | string | 否 | 字段类型 |
| comment | string | 是 | 注释 |
| tags | map,string,string | 是 | 自定义tag对 |
| NotNameValidation | bool | 否 | 不检查字段名合法性 |
| groups | list,string | 是 | 分组 |
| variants | list,string | 是 | 字段变体 |

## table

table是数据表的逻辑表示。table并非类型，不能用于field的type定义。

| 字段 | 类型 | 可空 | 描述 |
| --- | --- | --- | --- |
| namespace | string | 是 | 命名空间 |
| name | string | 否 | 类型名 |
| index | string | 是 | 索引字段列表 |
| mode | TableMode | 是 | 表模式(one/map/list) |
| valueType | string | 否 | 记录类型 |
| readSchemaFromFile | bool | 否 | 是否从inputFiles中解析valueType定义 |
| comment | string | 是 | 注释 |
| tags | map,string,string | 是 | 自定义tag对 |
| groups | list,string | 是 | 导出分组 |
| inputFiles | list,string | 否 | 输入的数据文件列表 |
| outputFileName | string | 是 | 输出的文件名 |

inputFiles指定了多个输入数据源，每个数据源可以是：
- 来自某个excel文件的所有单元薄。例如 xxx.xlsx
- 来自某个excel文件的指定单元薄。例如 sheet@xxx.xlsx
- 来自json、xml、lua、yaml文件。例如 xx.json
- 来自json、xml、lua、yaml子字段。例如 *items@item_module.json
- 来自目录。目录树下所有文件都会被当作数据源读入
- 以上的随意组合

## 公共属性

### groups

由导出table的valueType计算出所有直接或者间接引用的类型，称之为默认导出集合。如果某个类型在默认导出集合内，即使它的groups不属于当前导出目标，也会被导出。

如果groups中包含"*"，则表示属于所有分组。

如果table、bean、enum的groups为空，当导出target的groups中有任意一个group的default为true时，将导出这些类型，否则不导出这些类型。

### tags

tags主要有两个用途：校验器和特殊代码生成。has_tag函数用于检查是否有某个tag, get_tag用于获得某个tag对应的值。

### typeMapper

将配置类映射到外部现成的enum或者class类型。例如将vector3映射到UnityEngine.Vector3。

| 字段 | 类型 | 可空 | 描述 |
| --- | --- | --- | --- |
| targets | list,string | 否 | 匹配的输出目标 |
| codeTargets | list,string | 否 | 匹配的代码目标 |
| options | map,string,string | 是 | 生成需要的参数 |
