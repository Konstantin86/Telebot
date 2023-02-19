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

        internal BinanceFuturesUsdtExchangeInfo GetExchangeInfo()
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                try
                {
                    var exchangeInfo = client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync().GetAwaiter().GetResult();
                    return exchangeInfo.Data;
                }
                catch
                {
                    throw;
                }
            }
        }

        internal decimal GetFuturesAccountMargin()
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                try
                {
                    var accountInfo = client.UsdFuturesApi.Account.GetAccountInfoAsync().GetAwaiter().GetResult();
                    return accountInfo.Data.TotalMarginBalance;
                }
                catch
                {
                    throw;
                }
            }
        }

        internal void OpenLimitOrderShort(string symbol, decimal currentPrice)
        {
            using (var client = new BinanceClient(new BinanceClientOptions { ApiCredentials = new BinanceApiCredentials(this.apiKey, this.apiSecret) }))
            {
                try
                {
                    var tradeSymbolInfo = this.tradingConfig.BinanceExchangeInfo.Symbols.FirstOrDefault(m => m.BaseAsset.ToUpper() == symbol.ToAsset().ToUpper());
                    var quantity = Math.Round((this.tradingConfig.AssetTradedSideUsdt * this.tradingConfig.Leverage / currentPrice), tradeSymbolInfo.QuantityPrecision);
                    var price = currentPrice + (currentPrice * (this.tradingConfig.TargetProfitPercentage / this.tradingConfig.Leverage));

                    var orderResponse = client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, OrderSide.Sell, FuturesOrderType.Limit, quantity, price, PositionSide.Both, TimeInForce.GoodTillCanceled).GetAwaiter().GetResult();
                    
                    if (orderResponse.Success)
                    {
                        this.tradingState.State[symbol].LimitOrder = orderResponse.Data;
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
    }
}
