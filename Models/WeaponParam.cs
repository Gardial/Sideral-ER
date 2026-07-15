using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RandomMagicConversion.Models
{
    public class WeaponParam
    {
         public Dictionary<string, string> Fields { get; private set; }

        public WeaponParam()
        {
            Fields = new Dictionary<string, string>();
        }

        public string this[string key]
        {
            get => Fields.ContainsKey(key) ? Fields[key] : "";
            set => Fields[key] = value;
        }
        public string Id => Fields.ContainsKey("ID") ? Fields["ID"] : "";
    }
}