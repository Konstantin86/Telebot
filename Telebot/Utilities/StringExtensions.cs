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

        public static string ToBinanceSymbolChartLink(this string symbol)
        {
            return $"https://www.binance.com/en/futures/{symbol}";
        }

        public static string ToBinanceChartHyperLink(this string symbol)
        {
            return $"<a href =\"{symbol.ToBinanceSymbolChartLink()}\">{symbol}</a>";
        }

        public static string ToTradingViewAssetChartLink(this string asset, int intervalMins)
        {
            return $"https://www.tradingview.com/chart/?symbol=BINANCE:{asset}USDT&interval={intervalMins}";
        }
    }
}
