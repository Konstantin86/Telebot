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

        public BinanceClientService(string apiKey, string apiSecret, TradingConfig tradingConfig, TradingState tradingState)
        {
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;

            this.tradingState = tradingState;
            this.tradingConfig = tradingConfig;
        }

        public async Task OpenFuturesStream(Action<DataEvent<IBinanceStreamKlineData>> onKandleLineMessageCallback)
        {
            var pairs = new List<string>();

            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                var exchangeInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                pairs = exchangeInfo.Data.Symbols
                    .Where(m => m.Pair.EndsWith("USDT") && m.ContractType == ContractType.Perpetual && m.Status == SymbolStatus.Trading)
                    .Select(m => m.Pair).ToList();
            }

            using (var futuresSocketClient = new BinanceSocketClient())
            {
                try
                {
                    var updateSubcription = await futuresSocketClient.UsdFuturesStreams.SubscribeToKlineUpdatesAsync(pairs, KlineInterval.OneMinute, onKandleLineMessageCallback);
                }
                catch (Exception)
                {
                    await futuresSocketClient.UnsubscribeAllAsync();
                    throw;
                }
            }
        }

        internal async Task<string?> PlaceOrder(string symbol, decimal price, OrderSide side)
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                try
                {
                    var accountInfo = await client.UsdFuturesApi.Account.GetAccountInfoAsync();

                    bool maxTradesAreOpen = accountInfo.Data.Positions.Count(m => m.UnrealizedPnl != 0) >= this.tradingConfig.SymbolsInTrade;
                    bool symbolPositionIsOpen = (accountInfo.Data.Positions.FirstOrDefault(m => m.Symbol == symbol && m.UnrealizedPnl != 0) != null);
                    bool symbolPositionWasOpenWithinRecent24h = ((DateTime.Now - tradingState.State[symbol].LastOrderDate).TotalHours < 12);

                    if (maxTradesAreOpen || symbolPositionIsOpen || symbolPositionWasOpenWithinRecent24h) return null;

                    var tradeSymbolInfo = (await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync()).Data.Symbols.FirstOrDefault(m => m.BaseAsset.ToUpper() == symbol.ToAsset().ToUpper());

                    if (tradeSymbolInfo == null) return null;

                    var bookPrice = await client.UsdFuturesApi.ExchangeData.GetBookPriceAsync(symbol);
                    decimal orderPrice = (bookPrice.Data.BestAskPrice + bookPrice.Data.BestBidPrice) / 2;

                    if (side == OrderSide.Buy)
                    {
                        if (orderPrice > price + price * 0.01m)
                        {
                            return $"[{symbol}]: Price has promptly moved from {price.ToString("C5")} to {orderPrice.ToString("C5")}. Waiting for a better moment to open long position...";
                        }
                    }
                    else
                    {
                        if (orderPrice < price - price * 0.01m)
                        {
                            return $"[{symbol}]: Price has promptly moved from {price.ToString("C5")} to {orderPrice.ToString("C5")}. Waiting for a better moment to open short position...";
                        }
                    }

                    var quantity = Math.Round((((accountInfo.Data.TotalMarginBalance / this.tradingConfig.Leverage) * this.tradingConfig.TradedPercentage) / this.tradingConfig.SymbolsInTrade) * this.tradingConfig.Leverage / price, tradeSymbolInfo.QuantityPrecision);

                    await client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(symbol, this.tradingConfig.Leverage);

                    var orderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side, FuturesOrderType.Market, quantity);

                    if (orderResponse.Success)
                    {
                        tradingState.State[symbol].LastOrderDate = DateTime.Now;

                        var takeProfitOrderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell, FuturesOrderType.TakeProfitMarket, quantity, timeInForce: TimeInForce.GoodTillCanceled, stopPrice: Math.Round(side == OrderSide.Sell ? price - (price * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)) : price + (price * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)), tradeSymbolInfo.PricePrecision), closePosition: true);
                        var stopLossOrderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell, FuturesOrderType.StopMarket, quantity, timeInForce: TimeInForce.GoodTillCanceled, stopPrice: Math.Round(side == OrderSide.Sell ? price + (price * Convert.ToDecimal(tradingConfig.StopLossPercentage)) : price - (price * Convert.ToDecimal(tradingConfig.StopLossPercentage)), tradeSymbolInfo.PricePrecision), closePosition: true);

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

        public async Task<WebCallResult<IEnumerable<IBinanceKline>>> GetMarketData(string symbol, KlineInterval interval, DateTime? start = null, DateTime? end = null)
        {
            using (var client = new BinanceClient())
            {
                return await client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, start, end);
            }
        }

        public async Task<Dictionary<string, DateTime>> GetFuturesPairs()
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                var exchangeInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                return exchangeInfo.Data.Symbols.Where(m => m.Pair.EndsWith("USDT") && m.ContractType == ContractType.Perpetual && m.Status == SymbolStatus.Trading).ToDictionary(k => k.Pair, v => v.ListingDate);
            }
        }
    }
}
