using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Telebot.Trading;
using Telebot.Utilities;

namespace Telebot.Binance
{
    internal class BinanceClientService
    {
        private string apiKey;
        private string apiSecret;

        private TradingConfig tradingConfig;
        private TradingState tradingState;
        private BinanceSocketClient futuresSocketClient;

        public BinanceClientService(string apiKey, string apiSecret, TradingConfig tradingConfig, TradingState tradingState)
        {
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;

            this.tradingState = tradingState;
            this.tradingConfig = tradingConfig;
        }

        public void OpenFuturesStream(Action<DataEvent<IBinanceStreamKlineData>> onKandleLineMessageCallback)
        {
            var pairs = new List<string>();

            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                var exchangeInfo = client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync().GetAwaiter().GetResult();
                pairs = exchangeInfo.Data.Symbols
                    .Where(m => m.Pair.EndsWith("USDT") && m.ContractType == ContractType.Perpetual && m.Status == SymbolStatus.Trading)
                    .Select(m => m.Pair).ToList();
            }

            this.futuresSocketClient = new BinanceSocketClient();

            try
            {
                var updateSubcription = this.futuresSocketClient.UsdFuturesStreams.SubscribeToKlineUpdatesAsync(pairs, KlineInterval.OneMinute, onKandleLineMessageCallback).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                this.futuresSocketClient.UnsubscribeAllAsync().GetAwaiter().GetResult();
                this.futuresSocketClient.Dispose();
                throw;
            }
        }

        internal string PlaceOrder(string symbol, decimal price, OrderSide side)
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                try
                {
                    var accountInfo = client.UsdFuturesApi.Account.GetAccountInfoAsync().GetAwaiter().GetResult();

                    bool maxTradesAreOpen = accountInfo.Data.Positions.Count(m => m.UnrealizedPnl != 0) >= this.tradingConfig.SymbolsInTrade;
                    bool symbolPositionIsOpen = (accountInfo.Data.Positions.FirstOrDefault(m => m.Symbol == symbol && m.UnrealizedPnl != 0) != null);
                    bool symbolPositionWasOpenWithinRecent24h = ((DateTime.Now - tradingState.State[symbol].LastOrderDate).TotalHours < 24);

                    if (maxTradesAreOpen || symbolPositionIsOpen || symbolPositionWasOpenWithinRecent24h) return null;

                    var tradeSymbolInfo = client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync().GetAwaiter().GetResult().Data.Symbols.FirstOrDefault(m => m.BaseAsset.ToUpper() == symbol.ToAsset().ToUpper());

                    var quantity = Math.Round(((((accountInfo.Data.TotalMarginBalance / this.tradingConfig.Leverage) * this.tradingConfig.TradedPercentage) / this.tradingConfig.SymbolsInTrade) * this.tradingConfig.Leverage / price), tradeSymbolInfo.QuantityPrecision);

                    client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(symbol, this.tradingConfig.Leverage).GetAwaiter().GetResult();

                    var orderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side, FuturesOrderType.Market, quantity).GetAwaiter().GetResult();

                    if (orderResponse.Success)
                    {
                        tradingState.State[symbol].LastOrderDate = DateTime.Now;

                        var takeProfitOrderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                            symbol,
                            side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell,
                            FuturesOrderType.TakeProfitMarket,
                            quantity,
                            timeInForce: TimeInForce.GoodTillCanceled,
                            stopPrice: Math.Round(side == OrderSide.Sell ? price - (price * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)) : price + (price * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)), tradeSymbolInfo.PricePrecision),
                            closePosition: true
                            ).GetAwaiter().GetResult();

                        var stopLossOrderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                            symbol,
                            side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell,
                            FuturesOrderType.StopMarket,
                            quantity,
                            timeInForce: TimeInForce.GoodTillCanceled,
                            stopPrice: Math.Round(side == OrderSide.Sell ? price + (price * Convert.ToDecimal(tradingConfig.StopLossPercentage)) : price - (price * Convert.ToDecimal(tradingConfig.StopLossPercentage)), tradeSymbolInfo.PricePrecision),
                            closePosition: true
                        ).GetAwaiter().GetResult();

                        string tpMessage = takeProfitOrderResponse.Success ? "TP is automatically set to fix 1% of profit"
                            : $"TP wasn't set due to error{(takeProfitOrderResponse.Error != null ? $":{takeProfitOrderResponse.Error.Code}:{takeProfitOrderResponse.Error.Message}" : "")}";

                        string slMessage = stopLossOrderResponse.Success ? "SL is automatically set to fix 3% of loose"
                            : $"SL wasn't set due to error{(stopLossOrderResponse.Error != null ? $":{stopLossOrderResponse.Error.Code}:{stopLossOrderResponse.Error.Message}" : "")}";

                        return $"{symbol}: Opening {(side == OrderSide.Sell ? "Short" : "Long")} position at {price.ToString("C5")} " +
                                $"due to a huge deviation from MA20 being detected. " +
                                $"In most of the cases price will roll back at least 1%. {tpMessage}. {slMessage}." +
                                $"Please track the orders in your Binance application.";
                    }
                    else
                    {
                        return $"Error when opening {symbol} order: {(orderResponse.Error != null ? orderResponse.Error.Code + orderResponse.Error.Message : string.Empty)}";
                    }
                }
                catch (Exception ex) { return $"Exception when trying to opne {symbol} order: {ex}"; }
            }
        }

        public WebCallResult<IEnumerable<IBinanceKline>> GetMarketData(string symbol, KlineInterval interval, DateTime? start = null, DateTime? end = null)
        {
            using (var client = new BinanceClient())
            {
                WebCallResult<IEnumerable<IBinanceKline>> webCallResult = client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, start, end).GetAwaiter().GetResult();
                return webCallResult;
            }
        }

        public Dictionary<string, DateTime> GetFuturesPairs()
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                var exchangeInfo = client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync().GetAwaiter().GetResult();
                return exchangeInfo.Data.Symbols.Where(m => m.Pair.EndsWith("USDT") && m.ContractType == ContractType.Perpetual && m.Status == SymbolStatus.Trading).ToDictionary(k => k.Pair, v => v.ListingDate);
            }
        }
    }
}
