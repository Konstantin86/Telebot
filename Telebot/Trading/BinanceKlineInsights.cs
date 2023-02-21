using Binance.Net.Interfaces;
using Newtonsoft.Json;
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
        public BinanceKlineInsights() {}

        public BinanceKlineInsights(IBinanceKline binanceKline)
        {
            ClosePrice = Convert.ToDouble(binanceKline.ClosePrice);
            HighPrice = Convert.ToDouble(binanceKline.HighPrice);
            LowPrice = Convert.ToDouble(binanceKline.LowPrice);
            OpenTime = binanceKline.OpenTime;
        }

        public DateTime OpenTime { get; set; }
        public double ClosePrice { get; set; }
        public double LowPrice { get; set; }
        public double HighPrice { get; set; }
        [JsonIgnore]
        public int? CandlesTillProfit { get; set; }
        [JsonIgnore]
        public int? CandlesTillStopLoss { get; set; }
        public double MA20 { get; set; }
        public ChangeModel MAChange { get; set; }
    }

    internal class ChangeModel
    {
        public double Value { get; set; }
        public bool IsPositive { get; set; }

        [JsonIgnore]
        public double Abs => Math.Abs(Value);
    }
}
