using Binance.Net.Enums;
using Newtonsoft.Json;
using Telebot.Utilities;

namespace Telebot.Trading
{
    internal class SymbolTradeState
    {
        public Dictionary<KlineInterval, IntervalData> IntervalData { get; set; } = new Dictionary<KlineInterval, IntervalData>();
        public DateTime LastOrderDate { get; set; } = DateTime.MinValue;
        public DateTime LastInformDate { get; set; } = DateTime.MinValue;
        public double BinSize { get; set; } = 200;

        //[JsonIgnore]
        //public IEnumerable<BinanceKlineInsights> KlineInsightsOrderedByPerformance => KlineInsights.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs);

        // Fill out once at the application start after all symbols data loaded
        // Refresh after each candle closes (once per hour)
        // Provide telegram command to render info for btc or other symbol
        // Provide notification when a btc and any symbol (from open positions) price comes close to a level of resistance / support (these notificaitons should be not more frequent than 1 per 4 hours or in case if level breaks / changes)
        [JsonIgnore]
        public List<PriceBin> PriceBins { get; private set; }
        public bool IsMarkedOpen() => LastOrderDate > DateTime.MinValue;
        public void MarkClosed() => LastOrderDate = DateTime.MinValue;
        

        public void RefreshPriceBins(int recentDays = 90)
        {
            var klines = IntervalData[KlineInterval.OneHour].KlineInsights.TakeLast(recentDays * 24);
            var priceMin = klines.Select(k => k.LowPrice).Min();
            var priceMax = klines.Select(k => k.HighPrice).Max();

            BinSize = (priceMax - priceMin) / 50;

            var bins = Enumerable.Range(0, (int)((priceMax - priceMin) / BinSize)).Select(i => new PriceBin { Price = priceMin + i * BinSize }).ToList();

            foreach (var kline in klines)
            {
                var price = (kline.LowPrice + kline.HighPrice) / 2;
                var bin = bins.FirstOrDefault(b => b.Price <= price && price < b.Price + BinSize);
                if (bin != null) bin.Volume += kline.Volume;
            }

            PriceBins = bins.OrderByDescending(b => b.Price).ToList();

            foreach (var priceBin in PriceBins)
            {
                priceBin.Significance = priceBin.Volume.PercentileOf(PriceBins.Select(m => m.Volume).ToArray());
            }
        }
    }
}