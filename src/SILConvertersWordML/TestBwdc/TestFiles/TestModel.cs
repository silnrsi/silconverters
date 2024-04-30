using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
#if X64
using Paratext.PluginInterfaces;
using SIL.ParatextBackTranslationHelperPlugin;
#endif

namespace TestBwdc.TestFiles
{
    internal class TestModel
    {
        public static string ToJson<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            return json;
        }

#if X64
        public static Dictionary<string, List<IUSFMToken>> LoadKeyedListOfTokens(string embeddedResourceName)
        {
            var contents = LoadEmbeddedResourceFileAsStringExecutingAssembly(embeddedResourceName);
            var obj = JsonConvert.DeserializeObject<ExpandoObject>(contents);
            Dictionary<string, List<IUSFMToken>> map = obj.ToDictionary(p => p.Key, p => ToListTokens(p.Value));
            return map;
        }

        public static IVerseRef LoadVerseRef(string embeddedResourceName)
        {
            var contents = LoadEmbeddedResourceFileAsStringExecutingAssembly(embeddedResourceName);
            var obj = JsonConvert.DeserializeObject<ExpandoObject>(contents);
            return TestVerseReference.ToIVerseRef(obj);
        }

        public static Dictionary<string, SortedDictionary<string, List<IUSFMToken>>> LoadKeyedSortedListOfTokens(string embeddedResourceName)
        {
            var contents = LoadEmbeddedResourceFileAsStringExecutingAssembly(embeddedResourceName);
            var obj = JsonConvert.DeserializeObject<ExpandoObject>(contents);
            Dictionary<string, SortedDictionary<string, List<IUSFMToken>>> map = obj.ToDictionary(p => p.Key, p => ToKeyedSortedTokens(p.Value));
            return map;
        }

        private static SortedDictionary<string, List<IUSFMToken>> ToKeyedSortedTokens(object sDictionary)
        {
            var sortedDictionary = (IDictionary<string, object>)sDictionary;
            var keyedSortedTokens = new SortedDictionary<string, List<IUSFMToken>>();
            foreach (var kvp in sortedDictionary)
            {
                var key = kvp.Key;
                var value = ToListTokens(kvp.Value);
                keyedSortedTokens.Add(key, value);
            }
            return keyedSortedTokens;
        }

        public static List<IUSFMToken> ToListTokens(object value)
        {
            var expandoTokens = (List<System.Object>)value;
            var tokens = new List<IUSFMToken>();
            foreach (ExpandoObject expandoToken in expandoTokens.Cast<ExpandoObject>())
            {
                IUSFMToken usfmToken = null;
                if (((IDictionary<String, object>)expandoToken).ContainsKey("Text"))
                {
#if UseSpecialTestClasses
                    usfmToken = new TestTextToken(expandoToken);
#else
                    usfmToken = new TextToken(expandoToken);
#endif
                }
                else
                {
                    usfmToken = new MarkerToken(expandoToken);
                }
                tokens.Add(usfmToken);
            }
            return tokens;
        }
#endif

        public static T LoadFromFileJson<T>(string embeddedResourceName)
        {
            var contents = LoadEmbeddedResourceFileAsStringExecutingAssembly(embeddedResourceName);
            var obj = JsonConvert.DeserializeObject<T>(contents);
            return obj;
        }

        public static string LoadEmbeddedResourceFileAsStringExecutingAssembly(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.Contains(resourceName));
            if (String.IsNullOrEmpty(resourceName))
                return null;

            var resourceAsStream = assembly.GetManifestResourceStream(resourceName);
            StreamReader reader = new StreamReader(resourceAsStream);
            string text = reader.ReadToEnd();
            return text;
        }
    }
}
