using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.CommonObjects;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using System.Drawing;
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

        private BinanceSocketClient binanceSocketClient;

        public BinanceClientService(string apiKey, string apiSecret, TradingConfig tradingConfig, TradingState tradingState)
        {
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;

            this.tradingState = tradingState;
            this.tradingConfig = tradingConfig;
        }

        public async Task OpenFuturesStream(Action<DataEvent<IBinanceStreamKlineData>> onKandleLineMessageCallback, KlineInterval interval)
        {
            var pairs = new List<string>();

            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                var exchangeInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                pairs = exchangeInfo.Data.Symbols
                    .Where(m => m.Pair.EndsWith("USDT") && m.ContractType == ContractType.Perpetual && m.Status == SymbolStatus.Trading)
                    .Select(m => m.Pair).ToList();
            }

            var futuresSocketClient = new BinanceSocketClient();

            try
            {
                var updateSubcription = await futuresSocketClient.UsdFuturesStreams.SubscribeToKlineUpdatesAsync(pairs, interval, onKandleLineMessageCallback);
            }
            catch (Exception)
            {
                await futuresSocketClient.UnsubscribeAllAsync();
                futuresSocketClient.Dispose();
                throw;
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

                        decimal takeProfitStopPrice = Math.Round(Math.Round(side == OrderSide.Sell ? price - (price * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)) : price + (price * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)), tradeSymbolInfo.PricePrecision) / tradeSymbolInfo.PriceFilter.TickSize, MidpointRounding.ToEven) * tradeSymbolInfo.PriceFilter.TickSize;
                        decimal stopLossStopPrice = Math.Round(Math.Round(side == OrderSide.Sell ? price + (price * Convert.ToDecimal(tradingConfig.StopLossPercentage)) : price - (price * Convert.ToDecimal(tradingConfig.StopLossPercentage)), tradeSymbolInfo.PricePrecision) / tradeSymbolInfo.PriceFilter.TickSize, MidpointRounding.ToEven) * tradeSymbolInfo.PriceFilter.TickSize;

                        var takeProfitOrderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell, FuturesOrderType.TakeProfitMarket, quantity, timeInForce: TimeInForce.GoodTillCanceled, stopPrice: takeProfitStopPrice, closePosition: true);
                        var stopLossOrderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell, FuturesOrderType.StopMarket, quantity, timeInForce: TimeInForce.GoodTillCanceled, stopPrice: stopLossStopPrice, closePosition: true);

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

        internal async Task<List<string>> SetTakeProfitsWhereMissing()
        {
            var messages = new List<string>();

            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                var accountInfo = await client.UsdFuturesApi.Account.GetAccountInfoAsync();
                var ordersResponse = await client.UsdFuturesApi.Trading.GetOpenOrdersAsync();
                var tradeSymbolsInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();

                if (!accountInfo.Success)
                {
                    messages.Add($"GetAccountInfoAsync() calling error: {accountInfo.Error?.Message}");
                }

                if (!ordersResponse.Success)
                {
                    messages.Add($"GetOpenOrdersAsync() calling error: {ordersResponse.Error?.Message}");
                }

                if (!tradeSymbolsInfo.Success)
                {
                    messages.Add($"GetExchangeInfoAsync() calling error: {tradeSymbolsInfo.Error?.Message}");
                }

                var openPositions = accountInfo.Data.Positions.Where(m => m.UnrealizedPnl != 0);

                MarkObsoletePositionsAsClosed(openPositions);

                foreach (var openPosition in openPositions)
                {
                    if (ordersResponse.Success)
                    {
                        if (!ordersResponse.Data.Any(m => m.Symbol == openPosition.Symbol && (m.Type == FuturesOrderType.TakeProfit || m.Type == FuturesOrderType.TakeProfitMarket)))
                        {
                            var tradeSymbolInfo = tradeSymbolsInfo.Data.Symbols.FirstOrDefault(m => m.BaseAsset.ToUpper() == openPosition.Symbol.ToAsset().ToUpper());

                            var bookPrice = await client.UsdFuturesApi.ExchangeData.GetBookPriceAsync(openPosition.Symbol);
                            decimal orderPrice = (bookPrice.Data.BestAskPrice + bookPrice.Data.BestBidPrice) / 2;

                            OrderSide? orderSide = null;

                            if (openPosition.EntryPrice < bookPrice.Data.BestBidPrice && openPosition.EntryPrice < bookPrice.Data.BestAskPrice)
                            {
                                if (openPosition.UnrealizedPnl < 0)
                                {
                                    orderSide = OrderSide.Buy;
                                }
                                else if (openPosition.UnrealizedPnl > 0)
                                {
                                    orderSide = OrderSide.Sell;
                                }
                            }
                            else if (openPosition.EntryPrice > bookPrice.Data.BestBidPrice && openPosition.EntryPrice > bookPrice.Data.BestAskPrice)
                            {
                                if (openPosition.UnrealizedPnl < 0)
                                {
                                    orderSide = OrderSide.Sell;
                                }
                                else if (openPosition.UnrealizedPnl > 0)
                                {
                                    orderSide = OrderSide.Buy;
                                }
                            }

                            if (!orderSide.HasValue)
                            {
                                messages.Add($"[{openPosition.Symbol}]: Impossible to define current position side, please set take profit order manually with Binance application");
                                continue;
                            }

                            var stopPrice = Math.Round(Math.Round(orderSide == OrderSide.Sell
                                ? openPosition.EntryPrice + (openPosition.EntryPrice * Convert.ToDecimal(tradingConfig.TakeProfitPercentage))
                                : openPosition.EntryPrice - (openPosition.EntryPrice * Convert.ToDecimal(tradingConfig.TakeProfitPercentage)), tradeSymbolInfo.PricePrecision) / tradeSymbolInfo.PriceFilter.TickSize, MidpointRounding.ToEven) * tradeSymbolInfo.PriceFilter.TickSize;

                            var limitPrice = stopPrice;

                            var takeProfitOrderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                                openPosition.Symbol,
                                orderSide.Value,
                                FuturesOrderType.TakeProfitMarket,
                                Math.Round(openPosition.InitialMargin, tradeSymbolInfo.QuantityPrecision),
                                timeInForce: TimeInForce.GoodTillCanceled,
                                stopPrice: stopPrice,
                                closePosition: true);

                            if (!takeProfitOrderResponse.Success)
                            {
                                messages.Add($"[{openPosition.Symbol}]: {takeProfitOrderResponse.Error?.Message}, please set take profit order manually with Binance application");
                            }
                            else
                            {
                                messages.Add($"[{openPosition.Symbol}]: Take profit has been set!");
                            }
                        }
                    }
                }
            }

            return messages;
        }

        private void MarkObsoletePositionsAsClosed(IEnumerable<BinancePositionInfoUsdt> openPositions)
        {
            foreach (var tradingItem in tradingState.State)
            {
                if (tradingItem.Value.IsMarkedOpen() && openPositions.FirstOrDefault(m => m.Symbol == tradingItem.Key) == null)
                {
                    tradingItem.Value.MarkClosed();
                }
            }
        }
    }
}
