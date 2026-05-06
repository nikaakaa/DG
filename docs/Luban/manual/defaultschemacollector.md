# 配置定义 (DefaultSchemaCollector)

Luban有一套独立于具体实现的Schema逻辑结构。DefaultSchemaCollector提供了默认的配置定义格式。

## excel定义文件

enum、bean、table需要分别在不同的单元薄或者文件中定义。

### enum类型定义文件

| 字段 | 可空 | 默认值 | 说明 |
|---|---|---|---|
| full_name | 否 | | 类型全名 |
| flags | 是 | false | 是否为标志位类型 |
| unique | 是 | false | 枚举值是否唯一 |
| comment | 是 | | |
| tags | 是 | | key1=value1#key2=value2... |
| items | 否 | | 枚举项列表 |

枚举项字段：

| 字段 | 可空 | 默认值 | 说明 |
|---|---|---|---|
| name | 否 | | 枚举项名 |
| alias | 是 | | 别名 |
| value | 是 | | 枚举值，可以是10/16进制整数，也可以是A|B|C组合 |
| comment | 是 | | |
| tags | 是 | | |

### bean类型定义文件

| 字段 | 可空 | 默认值 | 说明 |
|---|---|---|---|
| full_name | 否 | | 类型全名 |
| parent | 是 | | 父类名 |
| valueType | 是 | false | 是否为值类型 |
| sep | 是 | | 默认分割符 |
| alias | 是 | | |
| comment | 是 | | |
| group | 是 | | |
| tags | 是 | | |
| fields | 否 | | 字段列表 |

字段定义：

| 字段 | 可空 | 默认值 | 说明 |
|---|---|---|---|
| name | 否 | | |
| type | 否 | | |
| group | 是 | | |
| comment | 是 | | |
| tags | 是 | | |

### table类型定义文件

| 字段 | 可空 | 默认值 | 说明 |
|---|---|---|---|
| full_name | 否 | | 类型全名 |
| value_type | 否 | | 表记录类型 |
| read_schema_from_file | 是 | false | 是否从input的excel文件标题头行读取value_type定义 |
| input | 否 | | 输入数据文件 |
| index | 是 | | 索引字段 |
| mode | 是 | | one/map/list |
| comment | 是 | | |
| group | 是 | | |
| tags | 是 | | |
| output | 否 | | 输出文件名 |

## xml定义文件

xml文件不需要区分类型，可以在一个文件中混合定义enum、bean、table。

```xml
<module name="item">
    <enum name="Quality">
        <var name="WHITE" alias="白"/>
        <var name="RED" alias="红"/>
    </enum>
    <bean name="Item">
        <var name="id" type="int"/>
        <var name="count" type="int"/>
    </bean>
    <table name="TbItem" value="Item" input="item.xlsx"/>
    <module name="subModule">
        <bean name="SubModuleType">
            <var name="id" type="int"/>
        </bean>
    </module>
</module>
```

### module定义

| 字段名 | 可选 | 描述 |
|---|---|---|
| name | 是 | 命名空间名，可以多级如a.b |

### enum定义 (xml)

| 字段名 | 可选 | 默认值 | 描述 |
|---|---|---|---|
| name | 否 | | 类型名，不能包含命名空间 |
| flags | 是 | false | |
| unique | 是 | false | |
| comment | 是 | | |
| tags | 是 | | |

子元素var定义枚举项，mapper定义外部类型映射。

### bean定义 (xml)

| 字段名 | 可选 | 默认值 | 描述 |
|---|---|---|---|
| name | 否 | | 类型名 |
| parent | 是 | | 父类名 |
| valueType | 是 | false | |
| sep | 是 | | |
| alias | 是 | | |
| comment | 是 | | |
| group | 是 | | |
| tags | 是 | | |

子元素：var（字段）、bean（子结构/多态）、mapper（类型映射）。

### table定义 (xml)

| 字段名 | 可选 | 默认值 | 描述 |
|---|---|---|---|
| name | 否 | | 类型名 |
| value | 否 | | 表记录类型 |
| readSchemaFromFile | 是 | false | |
| input | 否 | | 可多个，逗号分割 |
| index | 是 | | 联合主键用+分割，独立主键用,分割 |
| mode | 是 | | one/map/list |
| comment | 是 | | |
| group | 是 | | |
| tags | 是 | | |
| output | 否 | | |

### constalias

常量别名仅能用于数值类型，且仅在excel族和lite类型源数据中生效。

### refgroup

refgroup为DefaultSchemaCollector的语法糖，用于表示一组被ref引用的table。
