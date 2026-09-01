using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DanaProcessing
{
    /// <summary>
    /// A JSON object, equivalent to Processing's JSONObject —
    /// https://processing.org/reference/JSONObject.html. Thin wrapper around
    /// System.Text.Json.Nodes.JsonObject with Processing's Get*/Set* naming,
    /// so sketch code reads the same as the Processing reference. Get one via
    /// Sketch.LoadJSONObject(path) or `new JSONObject()`, then
    /// Sketch.SaveJSONObject(obj, path) to write it back out.
    /// </summary>
    public sealed class JSONObject
    {
        internal JsonObject Node { get; }

        public JSONObject() : this(new JsonObject()) { }
        internal JSONObject(JsonObject node) => Node = node;

        public bool HasKey(string key) => Node.ContainsKey(key);

        public string GetString(string key, string fallback = "") => Node[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;
        public int GetInt(string key, int fallback = 0) => Node[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
        public float GetFloat(string key, float fallback = 0f) => Node[key] is JsonValue v && v.TryGetValue<double>(out var d) ? (float)d : fallback;
        public double GetDouble(string key, double fallback = 0) => Node[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : fallback;
        public bool GetBoolean(string key, bool fallback = false) => Node[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
        public JSONObject? GetJSONObject(string key) => Node[key] is JsonObject o ? new JSONObject(o) : null;
        public JSONArray? GetJSONArray(string key) => Node[key] is JsonArray a ? new JSONArray(a) : null;

        public void SetString(string key, string value) => Node[key] = value;
        public void SetInt(string key, int value) => Node[key] = value;
        public void SetFloat(string key, float value) => Node[key] = value;
        public void SetDouble(string key, double value) => Node[key] = value;
        public void SetBoolean(string key, bool value) => Node[key] = value;

        /// <summary>Stores a deep copy of value under key — a copy, not the same live object, so later edits to value won't retroactively change what's stored here (matching plain JSON's value semantics).</summary>
        public void SetJSONObject(string key, JSONObject value) => Node[key] = value.Node.DeepClone();
        public void SetJSONArray(string key, JSONArray value) => Node[key] = value.Node.DeepClone();

        public override string ToString() => Node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        public static JSONObject Parse(string json) => new JSONObject(JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("El texto no es un objeto JSON válido."));

        public static JSONObject Load(string path) => Parse(File.ReadAllText(path));
        public void Save(string path) => File.WriteAllText(path, ToString());
    }

    /// <summary>
    /// A JSON array, equivalent to Processing's JSONArray —
    /// https://processing.org/reference/JSONArray.html.
    /// </summary>
    public sealed class JSONArray
    {
        internal JsonArray Node { get; }

        public JSONArray() : this(new JsonArray()) { }
        internal JSONArray(JsonArray node) => Node = node;

        public int Size() => Node.Count;

        public string GetString(int i) => Node[i] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
        public int GetInt(int i) => Node[i] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;
        public float GetFloat(int i) => Node[i] is JsonValue v && v.TryGetValue<double>(out var d) ? (float)d : 0f;
        public double GetDouble(int i) => Node[i] is JsonValue v && v.TryGetValue<double>(out var d) ? d : 0;
        public bool GetBoolean(int i) => Node[i] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : false;
        public JSONObject GetJSONObject(int i) => new JSONObject((JsonObject)Node[i]!);
        public JSONArray GetJSONArray(int i) => new JSONArray((JsonArray)Node[i]!);

        public void SetString(int i, string value) => Node[i] = value;
        public void AppendString(string value) => Node.Add(value);
        public void AppendInt(int value) => Node.Add(value);
        public void AppendFloat(float value) => Node.Add(value);
        public void AppendDouble(double value) => Node.Add(value);
        public void AppendBoolean(bool value) => Node.Add(value);
        public void AppendJSONObject(JSONObject value) => Node.Add(value.Node.DeepClone());
        public void AppendJSONArray(JSONArray value) => Node.Add(value.Node.DeepClone());

        public override string ToString() => Node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        public static JSONArray Parse(string json) => new JSONArray(JsonNode.Parse(json)?.AsArray()
            ?? throw new InvalidOperationException("El texto no es un arreglo JSON válido."));

        public static JSONArray Load(string path) => Parse(File.ReadAllText(path));
        public void Save(string path) => File.WriteAllText(path, ToString());
    }
}