using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Utilities
{
    public class IgnoreJsonPropertyResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (Attribute.IsDefined(member, typeof(JsonIgnoreAttribute)))
            {
                property.ShouldSerialize = instance => false;
            }

            return property;
        }
    }
}
