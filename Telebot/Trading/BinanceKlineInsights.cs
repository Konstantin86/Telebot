using Binance.Net.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Trading
{
    internal class BinanceKlineInsights
    {
        public BinanceKlineInsights(IBinanceKline binanceKline)
        {
            Kline = binanceKline;
        }

        public IBinanceKline Kline { get; }

        public decimal Spike => (Kline.HighPrice - Kline.OpenPrice) / Kline.OpenPrice;

        public ChangeModel? MASpike { get; set; }

        public ChangeModel? MADrop { get; set; }

        public ChangeModel? MAChange => MASpike.Value > MADrop.Value ? MASpike : MADrop;

        public int DaysTillProfit { get; internal set; }
    }

    internal class ChangeModel
    {
        public decimal Value { get; set; }

        public decimal Abs => Math.Abs(Value);

        public bool IsPositive => Value > 0;
    }
}
