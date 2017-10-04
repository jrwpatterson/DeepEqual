using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DeepEqual
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class ListComparison : IComparison
    {
        public IComparison Inner { get; private set; }

        public ListComparison(IComparison inner)
        {
            Inner = inner;
        }

        public bool CanCompare(Type type1, Type type2)
        {
            if (!ReflectionCache.IsListType(type1) || !ReflectionCache.IsListType(type2))
                return false;

            return CheckInnerCanCompare(type1, type2);
        }

        public ComparisonResult Compare(IComparisonContext context, object value1, object value2)
        {
            var list1 = ((IEnumerable)value1).Cast<object>().ToArray();
            var list2 = ((IEnumerable)value2).Cast<object>().ToArray();
            
            var results = new List<ComparisonResult>();
            int i = 0;
            i = CompareLists(context, list1, list2, results, i);
            CompareLists(context, list2, list1, results, i);
            
            if (list1.Length == 0)
            {
                return ComparisonResult.Pass;
            }

            return results.ToResult();
        }

        private int CompareLists(IComparisonContext context, object[] list1, object[] list2, List<ComparisonResult> results, int i)
        {
            foreach (var listItem in list1)
            {
                var matches = false;
                var item1 = SerializeObject(listItem, 1);
                object listIt2 = new object();
                foreach (var listItem2 in list2)
                {
                    var item2 = SerializeObject(listItem2, 1);
                    if (item1.Equals(item2))
                    {
                        listIt2 = listItem2;
                        matches = true;
                        break;
                    }
                }
                if (!matches)
                {
                    context.AddDifference(listItem, JsonConvert.SerializeObject(item1), "missing");
                    results.Add(ComparisonResult.Fail);
                }
                else
                {
                    var innerContext = context.VisitingIndex(i++);
                    results.Add(Inner.Compare(innerContext, listItem, listIt2));
                }
            }
            return i;
        }

        private bool CheckInnerCanCompare(Type listType1, Type listType2)
        {
            var type1 = ReflectionCache.GetEnumerationType(listType1);
            var type2 = ReflectionCache.GetEnumerationType(listType2);

            return Inner.CanCompare(type1, type2);
        }

        public static string SerializeObject(object obj, int maxDepth)
        {
            using (var strWriter = new StringWriter())
            {
                using (var jsonWriter = new CustomJsonTextWriter(strWriter))
                {
                    Func<bool> include = () => jsonWriter.CurrentDepth <= maxDepth;
                    var resolver = new CustomContractResolver(include);
                    var serializer = new JsonSerializer { ContractResolver = resolver };
                    serializer.Serialize(jsonWriter, obj);
                }
                return strWriter.ToString();
            }
        }
    }
    public class CustomJsonTextWriter : JsonTextWriter
    {
        public CustomJsonTextWriter(TextWriter textWriter) : base(textWriter) { }

        public int CurrentDepth { get; private set; }

        public override void WriteStartObject()
        {
            CurrentDepth++;
            base.WriteStartObject();
        }

        public override void WriteEndObject()
        {
            CurrentDepth--;
            base.WriteEndObject();
        }
    }

    public class CustomContractResolver : DefaultContractResolver
    {
        private readonly Func<bool> _includeProperty;

        public CustomContractResolver(Func<bool> includeProperty)
        {
            _includeProperty = includeProperty;
        }

        protected override JsonProperty CreateProperty(
            MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var shouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = obj => _includeProperty() &&
                                              (shouldSerialize == null ||
                                               shouldSerialize(obj));
            return property;
        }
    }
}