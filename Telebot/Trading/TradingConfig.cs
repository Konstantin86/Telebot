using Binance.Net.Objects.Models.Futures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Trading
{
    internal class TradingConfig
    {
        public decimal TradedPercentage { get; set; } = 1;
        public int Leverage { get; set; } = 20;
        public decimal TargetProfitPercentage { get; set; } = 0.2m;
        public int SymbolsInTrade { get; set; } = 2;
    }
}

