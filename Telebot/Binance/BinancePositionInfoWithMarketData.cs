using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Telebot.Binance
{
    public class BinancePositionInfoWithMarketData
    {
        public BinancePositionInfoUsdt PositionInfo { get; private set; }
        public WebCallResult<BinanceBookPrice> BookPrice { get; private set; }

        public IEnumerable<BinanceFuturesOrder> BinanceFuturesOrders { get; private set; }

        public BinancePositionInfoWithMarketData(
            BinancePositionInfoUsdt positionInfo, 
            WebCallResult<BinanceBookPrice> bookPrice, 
            IEnumerable<BinanceFuturesOrder> binanceFuturesOrders)
        {
            PositionInfo = positionInfo;
            BookPrice = bookPrice;
            BinanceFuturesOrders = binanceFuturesOrders;
        }

        public BinanceFuturesOrder TakeProfitOrder => BinanceFuturesOrders.FirstOrDefault(m => m.Type == FuturesOrderType.TakeProfit || m.Type == FuturesOrderType.TakeProfitMarket);

        public BinanceFuturesOrder StopLossOrder => BinanceFuturesOrders.FirstOrDefault(m => m.Type == FuturesOrderType.Stop || m.Type == FuturesOrderType.StopMarket);
    }
}
