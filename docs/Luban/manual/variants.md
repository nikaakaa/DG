# 字段变体（Variants）

版本：4.x

有时候同一个字段可能有多个配置。一个非常常见的场景是制作本地化数据时，不同地区的某个初始道具有不同的值。

### 定义

当前只有字段支持变体，所有可以定义字段的地方都可以定义变体信息。

#### xml定义

```xml
<bean name="TestVariant">
    <var name="id" type="int"/>
    <var name="value" type="int" variants="zh,en,fr,jp"/>
</bean>
```

#### 在 \_\_beans\_\_.xlsx中定义

在excel的beans定义表中，为字段添加variants列，填入变体名列表。

#### 在数据表的标题行定义

对于需要定义变体的字段，为每个变体定义一个`{fieldName}@{variant}`的变量。有几个规则：

* 变体标题头所在的列必须在变量名列之后，即 `value@en`必须在`value`列之后
* 不需要为变体变量定义`type`及`group`之类信息，变体变量直接使用原始变量相应的值，即`value@en`的`type`会取`value`的`type`。

### 配置变体数据

#### excel格式族

在数据表的标题行中为每个变体定义列，如 `value`、`value@en`、`value@zh`等。

#### json格式

```json
{
    "id":1,
    "value@en": 1001
}
```

#### xml格式

```xml
<data>
    <id>1</id>
    <value variant="en">1001</value>
</data>
```

#### yaml格式

```yaml
id: 1
value@en: 1001
```

#### lua格式

```lua
return {
    id=1,
    ["value@en"] = 1001,
}
```

### 导出数据

一般来说，既然定义了变量变体，导出数据时应该为该变量指定当前应该使用的变体。命令参数`--variant {variantKey}={variantName}`用于为字段指定当前使用的变体。 `{variantKey}`为`{beanFullName}.{fieldName}`, `{variantName}`为变体名。 例如`--variant TestVariant.value=en`表示导出数据时value字段取`value@en`列对应的值。

在为某个变量指定了当前使用的变体后，如果对应的字段不存在，则取默认字段的值。同样地，以变体en为例，如果`value@en`列不存在，则取`value`列的值，如果`value`列也不存在，则抛出字段找不到的错误。

可以使用`--variant default={variantName}`设置全局默认变体，当未没有为某个bean单独设置变体时，则取全局默认变体。

如果某个字段定义了变体，但没有在命令行中使用`--variant`指定该字段使用的变体名，则读取不含变量的原始变量的值。此时Luban会打印一行警告日志。
