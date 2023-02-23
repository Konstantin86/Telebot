using Newtonsoft.Json;
using Telebot.Utilities;

namespace Telebot.Trading
{
    internal class SymbolTradeState
    {
        public List<BinanceKlineInsights> KlineInsights { get; set; } = new List<BinanceKlineInsights>();
        
        
        // Fill out once at the application start after all symbols data loaded
        // Refresh after each candle closes (once per hour)
        // Provide telegram command to render info for btc or other symbol
        // Provide notification when a btc and any symbol (from open positions) price comes close to a level of resistance / support (these notificaitons should be not more frequent than 1 per 4 hours or in case if level breaks / changes)
        [JsonIgnore]        
        public List<PriceBin> PriceBins { get; private set; }

        public double BinSize { get; set; } = 200;

        public void RefreshPriceBins(int recentDays = 90)
        {
            var klines = KlineInsights.TakeLast(recentDays * 24);
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

        [JsonIgnore]
        public IEnumerable<BinanceKlineInsights> KlineInsightsOrderedByPerformance => KlineInsights.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs);

        public DateTime LastOrderDate { get; set; } = DateTime.MinValue;
        public DateTime LastInformDate { get; set; } = DateTime.MinValue;

        public double GetProfitability(double topMovesPercent)
        {
            return (double)KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Where(m => m.CandlesTillStopLoss == null && m.CandlesTillProfit != null).Count() 
                / KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Count();
        }

        public double GetCurrentMaChangeHistoricalPercentage()
        {
            return KlineInsights.Last().MAChange.Abs.PercentileOf(KlineInsightsOrderedByPerformance.Select(m => m.MAChange.Abs).ToArray());
        }

        public List<BinanceKlineInsights> GetStopLossCases(double topMovesPercent)
        {
            return KlineInsightsOrderedByPerformance
                .Take((int)(KlineInsights.Count * topMovesPercent))
                .Where(m => m.CandlesTillStopLoss != null).ToList();
        }

        public double GetProfitAverageTimeInCandles(double topMovesPercent)
        {
            var list = KlineInsightsOrderedByPerformance
                .Take((int)(KlineInsights.Count * topMovesPercent))
                .Where(m => m.CandlesTillProfit.HasValue);

            return list.Count() > 0 ? 
                list.Average(m => m.CandlesTillProfit.GetValueOrDefault())
                : 1;
        }

        internal bool EnterSpikeDetected(ChangeModel maChange, double topMovesPercent)
        {
            if (KlineInsights.Count * topMovesPercent < 1)
            {
                return false;
            }

            return maChange.Abs >= KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Average(m => m.MAChange.Abs);
        }


        internal bool InformSpikeDetected(ChangeModel maChange, double topMovesPercent)
        {
            if (KlineInsights.Count * topMovesPercent < 1)
            {
                return false;
            }

            return maChange.Abs >= KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Last().MAChange.Abs;
        }
    }
}