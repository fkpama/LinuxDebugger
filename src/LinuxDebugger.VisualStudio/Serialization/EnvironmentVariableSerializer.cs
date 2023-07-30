using System.Collections.Specialized;
using Newtonsoft.Json;

namespace LinuxDebugger.VisualStudio.Serialization
{
    internal sealed class ArgumentsConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(string[])
            || objectType == typeof(List<string>);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if (value is IEnumerable<string> collection)
            {
                foreach (var item in collection)
                {
                    writer.WriteValue(item);
                }
            }
            writer.WriteEndArray();
        }
    }
    internal sealed class EnvironmentVariableConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<string, string>)
                || objectType == typeof(Dictionary<string, object>)
                || objectType == typeof(NameValueCollection)
                || objectType == typeof(KeyValuePair<string, string>[])
                || objectType == typeof(KeyValuePair<string, object>[])
                ;
        }

        public override object? ReadJson(JsonReader reader,
                                         Type objectType,
                                         object? existingValue,
                                         JsonSerializer serializer)
        {
            //if (existingValue is null)
            //    return null;
            //var array = JArray.FromObject(existingValue);
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer,
                                       object? value,
                                       JsonSerializer serializer)
        {
            writer.WriteStartObject();
            if (value is not null)
            {
                if (value is IEnumerable<KeyValuePair<string, string>> ar1)
                {
                    writeArray(writer, ar1);
                }
                else if (value is IEnumerable<KeyValuePair<string, object>> ar2)
                {
                    writeArray(writer, ar2);
                }
                else if (value is NameValueCollection col)
                {
                    foreach (var name in col.AllKeys)
                    {
                        var val = col[name];
                        writer.WritePropertyName(name);

                        if (!string.IsNullOrWhiteSpace(val))
                            writer.WriteValue(val);
                        else
                            writer.WriteNull();
                    }
                }
                else
                {
                    throw new NotSupportedException($"Cannot convert from type {value.GetType().Name}");
                }
            }

            writer.WriteEndObject();

            static void writeArray<T>(JsonWriter writer, IEnumerable<KeyValuePair<string, T>> ar1)
            {
                foreach (var item in ar1)
                {
                    //writer.WriteStartObject();
                    writer.WritePropertyName(item.Key);
                    if (item.Value is null
                        || (item.Value is string s && string.IsNullOrWhiteSpace(s)))
                        writer.WriteNull();
                    else
                        writer.WriteValue(item.Value);
                    //writer.WriteEndObject();
                }
            }
        }
    }
}
