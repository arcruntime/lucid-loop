using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

namespace LucidLoop.Gyms.Editor
{
    // Pure XML helper also exercised outside Unity. No broad ATS bypass and no Bonjour discovery.
    public static class IosLocalRelayPlist
    {
        static readonly string[] LocalRanges = { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
            "169.254.0.0/16", "fc00::/7", "fe80::/10" };

        public static string Apply(string xml, bool development)
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            var document = XDocument.Load(reader);
            var root = document.Root?.Element("dict") ?? throw new ArgumentException("Invalid Info.plist");
            var ats = Dictionary(root, "NSAppTransportSecurity");
            Set(ats, "NSAllowsArbitraryLoads", new XElement("false"));
            Set(ats, "NSAllowsLocalNetworking", new XElement(development ? "true" : "false"));
            var exceptions = Dictionary(ats, "NSExceptionDomains");
            foreach (string range in LocalRanges)
            {
                Remove(exceptions, range);
                if (development) Set(exceptions, range, new XElement("dict",
                    new XElement("key", "NSExceptionAllowsInsecureHTTPLoads"), new XElement("true")));
            }
            if (!exceptions.HasElements) Remove(ats, "NSExceptionDomains");
            // User-entered local addresses can also use TLS; the privacy declaration is
            // appropriate in release as well and does not itself permit insecure traffic.
            Set(root, "NSLocalNetworkUsageDescription", new XElement("string",
                "Connect to the Lucid Loop game relay on your local network during development and testing."));
            return document.ToString();
        }

        static XElement Dictionary(XElement parent, string key)
        {
            var value = Value(parent, key);
            if (value == null) { value = new XElement("dict"); Set(parent, key, value); }
            if (value.Name != "dict") throw new ArgumentException("Invalid plist dictionary: " + key);
            return value;
        }
        static XElement Value(XElement parent, string key)
            => parent.Elements("key").FirstOrDefault(element => element.Value == key)?.ElementsAfterSelf().FirstOrDefault();
        static void Remove(XElement parent, string key)
        {
            foreach (var element in parent.Elements("key").Where(element => element.Value == key).ToArray())
            { element.ElementsAfterSelf().FirstOrDefault()?.Remove(); element.Remove(); }
        }
        static void Set(XElement parent, string key, XElement value)
        { Remove(parent, key); parent.Add(new XElement("key", key), value); }
    }

#if UNITY_EDITOR
    public sealed class IosLocalRelayPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 900;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string path = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(path)) throw new BuildFailedException("The exported iOS Info.plist was not found.");
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            File.WriteAllText(path, IosLocalRelayPlist.Apply(File.ReadAllText(path), development));
        }
    }
#endif
}
