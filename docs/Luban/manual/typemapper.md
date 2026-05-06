# 类型映射

有时候你希望生成的代码中能直接使用现成的结构类型，而不是使用生成的类型代码。例如将配置中的vector3映射到UnityEngine.Vector3。Luban支持这种外部类型映射机制。

目前**只有C#代码(cs-bin、cs-xxx-json之类)**支持类型映射。如果其他语言也需要类型，请仿照修改即可。

## enum类型映射

以AudioType为例：
```xml
<enum name="AudioType">
    <var name="UNKNOWN" value="0"/>
    <var name="ACC" value="1"/>
    <var name="AIFF" value="2"/>
    <mapper target="client" codeTarget="cs-bin">
        <option name="type" value="UnityEngine.AudioType"/>
    </mapper>
</enum>
```

name为'type'的option配置的value字段指定了类型映射的目标C#类型。必须保证枚举项的值与映射的枚举类型的枚举项的值完全一致，因为enum的类型映射的实现方式为先读取出配置AudioType，再类型强转为UnityEngine.AudioType。

## bean映射

以vector2、vector3、vector4为例：
```xml
<bean name="vector2" valueType="1" sep=",">
    <var name="x" type="float"/>
    <var name="y" type="float"/>
    <mapper target="client" codeTarget="cs-bin">
        <option name="type" value="UnityEngine.Vector2"/>
        <option name="constructor" value="ExternalTypeUtil.NewVector2"/>
    </mapper>
</bean>
```

bean映射相比enum需要提供一个自定义的强转（或者构造）函数，由'constructor'配置项提供。

## 区分target

对于不同的target，即使是同一种语言，前后端不一定使用相同的类型映射。使用 mapper的target和codeTarget参数组合来表达映射需求。当命令行的`-t $target`参数与`-c $codeTarget`参数分别与mapper的target及codeTarget的值匹配时，表示需要执行当前mapper指定的映射。

mapper的target和codeTarget参数都可以是多个值，如target="client,server,all"，codeTarget="cs-bin,cs-dotnet-json"。`-t`和`-c`参数只需要是其中一个即可满足匹配。
