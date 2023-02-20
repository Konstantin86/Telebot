using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telebot.Trading;
using Telebot.Utilities;
using Telegram.Bot.Types.Payments;

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

        internal void PlaceOrder(string symbol, decimal price, OrderSide side)
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                try
                {
                    var accountInfo = client.UsdFuturesApi.Account.GetAccountInfoAsync().GetAwaiter().GetResult();

                    if (accountInfo.Data.Positions.Count(m => m.UnrealizedPnl != 0) >= this.tradingConfig.SymbolsInTrade) return;

                    var tradeSymbolInfo = client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync().GetAwaiter().GetResult().Data.Symbols.FirstOrDefault(m => m.BaseAsset.ToUpper() == symbol.ToAsset().ToUpper());

                    var quantity = Math.Round(((((accountInfo.Data.TotalMarginBalance / this.tradingConfig.Leverage) * this.tradingConfig.TradedPercentage) / this.tradingConfig.SymbolsInTrade) * this.tradingConfig.Leverage / price), tradeSymbolInfo.QuantityPrecision);

                    client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(symbol, this.tradingConfig.Leverage).GetAwaiter().GetResult();
              
                    var orderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, side, FuturesOrderType.Market, quantity).GetAwaiter().GetResult();

                    if (orderResponse.Success)
                    {
                        var takeProfitOrderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                            symbol,
                            side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell,
                            FuturesOrderType.TakeProfitMarket,
                            quantity,
                            timeInForce: TimeInForce.GoodTillCanceled,
                            stopPrice: Math.Round(side == OrderSide.Sell ? price - (price * 0.01m) : price + (price * 0.01m), tradeSymbolInfo.PricePrecision),
                            closePosition: true
                            ).GetAwaiter().GetResult();

                        var stopLossOrderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(
                            symbol,
                            side == OrderSide.Sell ? OrderSide.Buy : OrderSide.Sell,
                            FuturesOrderType.StopMarket,
                            quantity,
                            timeInForce: TimeInForce.GoodTillCanceled,
                            stopPrice: Math.Round(side == OrderSide.Sell ? price + (price * 0.03m) : price - (price * 0.03m), tradeSymbolInfo.PricePrecision),
                            closePosition: true
                            ).GetAwaiter().GetResult();

                        //this.tradingState.State[symbol].LimitOrder = orderResponse.Data;
                    }
                }
                catch (Exception ex)
                {
                }
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
