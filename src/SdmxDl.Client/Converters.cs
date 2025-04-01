using SdmxDl.Client.Models;

namespace SdmxDl.Client;

public static class Converters
{
    public static Query ToModel(this Sdmxdl.Format.Protobuf.Query input)
    {
        return new Query() { Key = input.Key, Detail = (Detail)input.Detail };
    }

    public static Obs ToModel(this Sdmxdl.Format.Protobuf.Obs input)
    {
        return new Obs()
        {
            Period = input.Period,
            Value = input.Value,
            Meta = input.Meta.Map(x => (x.Key, x.Value)).ToHashMap()
        };
    }

    public static Series ToModel(this Sdmxdl.Format.Protobuf.Series input)
    {
        return new Series()
        {
            Key = input.Key,
            Meta = input.Meta.Map(x => (x.Key, x.Value)).ToHashMap(),
            Obs = input.Obs.Map(o => o.ToModel()).ToSeq().Strict()
        };
    }

    public static DataSet ToModel(this Sdmxdl.Format.Protobuf.DataSet input)
    {
        return new DataSet()
        {
            Ref = input.Ref,
            Query = input.Query.ToModel(),
            Data = input.Data.Map(s => s.ToModel()).ToSeq().Strict()
        };
    }
}
