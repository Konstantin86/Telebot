﻿using Newtonsoft.Json;
using Telebot.Utilities;

namespace Telebot.Trading
{
    public class IntervalData
    {
        public List<BinanceKlineInsights> KlineInsights { get; set; } = new List<BinanceKlineInsights>();
        
        [JsonIgnore]
        public IEnumerable<BinanceKlineInsights> KlineInsightsOrderedByPerformance => KlineInsights.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs);

        public double GetProfitability(double topMovesPercent)
        {
            return (double)KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Where(m => m.CandlesTillStopLoss == null && m.CandlesTillProfit != null).Count()
                / KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Count();
        }


        public double GetCurrentMaChangeHistoricalPercentage()
        {
            if (KlineInsights.Last().MAChange == null) return 1;

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

        public bool EnterSpikeDetected(ChangeModel maChange, double topMovesPercent)
        {
            if (KlineInsights.Count * topMovesPercent < 1)
            {
                return false;
            }

            return maChange.Abs >= KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Average(m => m.MAChange.Abs);
        }


        public bool InformSpikeDetected(ChangeModel maChange, double topMovesPercent)
        {
            if (KlineInsights.Count * topMovesPercent < 1)
            {
                return false;
            }

            return maChange.Abs >= KlineInsightsOrderedByPerformance.Take((int)(KlineInsights.Count * topMovesPercent)).Last().MAChange.Abs;
        }
    }
}