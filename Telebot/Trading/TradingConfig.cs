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
        public decimal AccountSize { get; set; }
        public decimal TradedPercentage { get; set; } = 1;
        public decimal Leverage { get; set; } = 20;
        public decimal TargetProfitPercentage { get; set; } = 0.2m;
        public int SymbolsInTrade { get; set; } = 2;

        public decimal AssetTradedSideUsdt => ((AccountSize / Leverage) * TradedPercentage) / SymbolsInTrade;
        public decimal GressProfitUsdt => AssetTradedSideUsdt * TargetProfitPercentage;

        public BinanceFuturesUsdtExchangeInfo BinanceExchangeInfo { get; set; }

    }
}

