using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Utilities
{
    public static class StringExtensions
    {
        public static string ToAsset(this string symbol)
        {
            return string.IsNullOrEmpty(symbol) ? symbol : symbol.Substring(0, symbol.Length - 4);
        }

        public static string ToSymbol(this string asset)
        {
            return asset.ToUpper() + "USDT";
        }
    }
}
