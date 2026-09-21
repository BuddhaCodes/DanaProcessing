using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DanaProcessing
{
    /// <summary>
    /// An XML element, equivalent to Processing's XML class —
    /// https://processing.org/reference/XML.html. Thin wrapper around
    /// System.Xml.Linq.XElement with Processing's Get*/Set*/GetChildren
    /// naming. Get the root via Sketch.LoadXML(path) or `new XML(name)`,
    /// then Sketch.SaveXML(xml, path) to write it back out.
    /// </summary>
    public sealed class XML
    {
        internal XElement Element { get; }

        /// <summary>Creates a new, empty element with the given tag name — the starting point for building an XML document from scratch, like Processing's `new XML(name)`.</summary>
        public XML(string name) : this(new XElement(name)) { }
        internal XML(XElement element) => Element = element;

        public string Name => Element.Name.LocalName;

        /// <summary>The element's text content, like Processing's getContent(). Doesn't include child elements' own text, only this element's direct text nodes.</summary>
        public string GetContent(string fallback = "") => Element.Nodes().OfType<XText>().FirstOrDefault()?.Value ?? fallback;
        public void SetContent(string text) => Element.Value = text;

        public bool HasAttribute(string name) => Element.Attribute(name) != null;
        public string GetString(string attribute, string fallback = "") => (string?)Element.Attribute(attribute) ?? fallback;
        public int GetInt(string attribute, int fallback = 0) => int.TryParse(GetString(attribute), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        public float GetFloat(string attribute, float fallback = 0f) => float.TryParse(GetString(attribute), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        public void SetString(string attribute, string value) => Element.SetAttributeValue(attribute, value);
        public void SetInt(string attribute, int value) => Element.SetAttributeValue(attribute, value.ToString(CultureInfo.InvariantCulture));
        public void SetFloat(string attribute, float value) => Element.SetAttributeValue(attribute, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>All direct child elements, like Processing's getChildren().</summary>
        public XML[] GetChildren() => Element.Elements().Select(e => new XML(e)).ToArray();

        /// <summary>Direct child elements matching a tag name, like Processing's getChildren(name).</summary>
        public XML[] GetChildren(string name) => Element.Elements(name).Select(e => new XML(e)).ToArray();

        /// <summary>The first direct child element matching a tag name, or null if there isn't one, like Processing's getChild(name).</summary>
        public XML? GetChild(string name)
        {
            var e = Element.Element(name);
            return e == null ? null : new XML(e);
        }

        /// <summary>Creates and appends a new child element, like Processing's addChild(name).</summary>
        public XML AddChild(string name)
        {
            var child = new XElement(name);
            Element.Add(child);
            return new XML(child);
        }

        public static XML Load(string path) => new XML(XElement.Load(path));
        public void Save(string path) => Element.Save(path);

        public override string ToString() => Element.ToString();
    }
}