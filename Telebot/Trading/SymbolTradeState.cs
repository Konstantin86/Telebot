using Newtonsoft.Json;

namespace Telebot.Trading
{
    internal class SymbolTradeState
    {
        public List<BinanceKlineInsights> KlineInsights { get; set; } = new List<BinanceKlineInsights>();

        [JsonIgnore]
        public IEnumerable<BinanceKlineInsights> KlineInsightsOrderedByPerformance => KlineInsights.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs);

        public DateTime LastOrderDate { get; set; } = DateTime.MinValue;

        public double GetProfitability(double topMovesPercent)
        {
            return (double)KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Where(m => m.CandlesTillStopLoss == null && m.CandlesTillProfit != null).Count() 
                / KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Count();
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

        internal bool SpikeDetected(ChangeModel maChange, double topMovesPercent)
        {
            if (KlineInsights.Count * topMovesPercent < 1)
            {
                return false;
            }

            return maChange.Abs >= KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Last().MAChange.Abs;
        }
    }
}