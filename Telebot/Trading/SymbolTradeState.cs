using Binance.Net.Objects.Models.Futures;

namespace Telebot.Trading
{
    internal class SymbolTradeState
    {
        public DateTime LastUpdatedOn { get; set; } = DateTime.Now;
        public BinanceFuturesPlacedOrder LimitOrder { get; set; }
    }
}