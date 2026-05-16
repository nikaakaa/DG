using Newtonsoft.Json.Linq;

namespace cfg.gamecore
{
public partial class TbEffectSpec
{
    private readonly System.Collections.Generic.Dictionary<string, gamecore.EffectSpec> _dataMap;
    private readonly System.Collections.Generic.List<gamecore.EffectSpec> _dataList;

    public TbEffectSpec(JArray _buf)
    {
        _dataMap = new System.Collections.Generic.Dictionary<string, gamecore.EffectSpec>(_buf.Count);
        _dataList = new System.Collections.Generic.List<gamecore.EffectSpec>(_buf.Count);
        foreach (JObject _ele in _buf)
        {
            gamecore.EffectSpec _v = global::cfg.gamecore.EffectSpec.DeserializeEffectSpec(_ele);
            _dataList.Add(_v);
            _dataMap.Add(_v.EffectId, _v);
        }
    }

    public System.Collections.Generic.IReadOnlyDictionary<string, gamecore.EffectSpec> DataMap => _dataMap;
    public System.Collections.Generic.IReadOnlyList<gamecore.EffectSpec> DataList => _dataList;

    public gamecore.EffectSpec GetOrDefault(string key) => _dataMap.TryGetValue(key, out var v) ? v : default;
    public gamecore.EffectSpec Get(string key) => _dataMap[key];
    public gamecore.EffectSpec this[string key] => _dataMap[key];

    public void ResolveRef(Tables tables)
    {
        foreach (var _v in _dataList)
        {
            _v.ResolveRef(tables);
        }
    }
}
}
