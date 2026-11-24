using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Unity tiplerini (Vector3, Quaternion, Color...) JSON'a sade biçimde yazar: {"x":1,"y":2,"z":3}.
    ///
    /// Neden gerekli? Newtonsoft, Vector3'ü yazarken "normalized" gibi özellikleri de yazmaya çalışır;
    /// normalized da bir Vector3 olduğu için sonsuz döngüye girer ("Self referencing loop" hatası).
    /// </summary>
    public static class UnityJsonConverters
    {
        public static IList<JsonConverter> All => new JsonConverter[]
        {
            new Vector2Converter(), new Vector3Converter(), new Vector4Converter(),
            new Vector2IntConverter(), new Vector3IntConverter(),
            new QuaternionConverter(), new ColorConverter(),
        };

        // Her dönüştürücü: alanları sırayla yaz, okurken aynı isimlerden doldur.
        abstract class FloatsConverter<T> : JsonConverter<T>
        {
            protected abstract string[] Names { get; }
            protected abstract float[] ToArray(T value);
            protected abstract T FromArray(float[] v);

            public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
            {
                var values = ToArray(value);
                writer.WriteStartObject();
                for (int i = 0; i < Names.Length; i++)
                {
                    writer.WritePropertyName(Names[i]);
                    writer.WriteValue(values[i]);
                }
                writer.WriteEndObject();
            }

            public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var values = ToArray(default);
                if (reader.TokenType == JsonToken.Null) return FromArray(values);
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) continue;
                    int index = Array.IndexOf(Names, (string)reader.Value);
                    reader.Read();
                    if (index >= 0) values[index] = Convert.ToSingle(reader.Value);
                }
                return FromArray(values);
            }
        }

        static readonly string[] XY = { "x", "y" }, XYZ = { "x", "y", "z" }, XYZW = { "x", "y", "z", "w" }, RGBA = { "r", "g", "b", "a" };

        class Vector2Converter : FloatsConverter<Vector2>
        {
            protected override string[] Names => XY;
            protected override float[] ToArray(Vector2 v) => new[] { v.x, v.y };
            protected override Vector2 FromArray(float[] v) => new Vector2(v[0], v[1]);
        }

        class Vector3Converter : FloatsConverter<Vector3>
        {
            protected override string[] Names => XYZ;
            protected override float[] ToArray(Vector3 v) => new[] { v.x, v.y, v.z };
            protected override Vector3 FromArray(float[] v) => new Vector3(v[0], v[1], v[2]);
        }

        class Vector4Converter : FloatsConverter<Vector4>
        {
            protected override string[] Names => XYZW;
            protected override float[] ToArray(Vector4 v) => new[] { v.x, v.y, v.z, v.w };
            protected override Vector4 FromArray(float[] v) => new Vector4(v[0], v[1], v[2], v[3]);
        }

        class Vector2IntConverter : FloatsConverter<Vector2Int>
        {
            protected override string[] Names => XY;
            protected override float[] ToArray(Vector2Int v) => new float[] { v.x, v.y };
            protected override Vector2Int FromArray(float[] v) => new Vector2Int(Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[1]));
        }

        class Vector3IntConverter : FloatsConverter<Vector3Int>
        {
            protected override string[] Names => XYZ;
            protected override float[] ToArray(Vector3Int v) => new float[] { v.x, v.y, v.z };
            protected override Vector3Int FromArray(float[] v) => new Vector3Int(Mathf.RoundToInt(v[0]), Mathf.RoundToInt(v[1]), Mathf.RoundToInt(v[2]));
        }

        class QuaternionConverter : FloatsConverter<Quaternion>
        {
            protected override string[] Names => XYZW;
            protected override float[] ToArray(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
            protected override Quaternion FromArray(float[] v) => new Quaternion(v[0], v[1], v[2], v[3]);
        }

        class ColorConverter : FloatsConverter<Color>
        {
            protected override string[] Names => RGBA;
            protected override float[] ToArray(Color c) => new[] { c.r, c.g, c.b, c.a };
            protected override Color FromArray(float[] v) => new Color(v[0], v[1], v[2], v[3]);
        }
    }
}
