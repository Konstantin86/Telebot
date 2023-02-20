using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using System.Linq;
using System.Timers;
using Telebot.Binance;
using Telebot.Trading;
using Telebot.Trading.TA;
using Telebot.Utilities;

namespace Telebot
{
    internal class Program
    {
        static Telegram.Telebot bot = null;
        static BinanceClientService binanceClientService;
        static TradingState tradingState = new TradingState();
        static TradingConfig tradingConfig = new TradingConfig();




        static void Main(string[] args)
        {
            InitBinanceClient();

            var symbols = binanceClientService.GetFuturesPairs().Where(m => m.Value < DateTime.UtcNow.AddDays(-5));

            foreach (var symbol in symbols.Select(m => m.Key))
            {
                RunBackTest(symbol);
            }

            //RunBackTest("BTCUSDT");
            //RunBackTest("ALGOUSDT");
            //RunBackTest("LDOUSDT");
            //RunBackTest("UNFIUSDT");

            Console.ReadLine();

            RunTelegramBot();

            // todo add timer job to close orphant TP / SL orders once a minute
        }

        private static void InitBinanceClient()
        {
            binanceClientService = new BinanceClientService("gvNqiHE4DJKhSREACPghpwSb9zrXaObCIriMJAZN1J0ptfLY8cLexZpqkXJGqD0s", "S1mMv1ZXUOWyWz6eEJCtZe23Pxvyx7As51EfVniJtmKXGQTClD7jxnHvs0W6XXnK", tradingConfig, tradingState);
         //   binanceClientService.OpenFuturesStream(HandleSymbolUpdate);
        }

        private static void RunBackTest(string symbol)
        {
            var marketData = new List<BinanceKlineInsights>();

            int i = 40;

            while (i > 0)
            {
                var start = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, 500 * i);
                var end = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, 500 * i).AddHours(500);

                var result = binanceClientService.GetMarketData(symbol, KlineInterval.OneHour, start, end);
                marketData.AddRange(result.Data.Select(m => new BinanceKlineInsights(m)));

                i--;
            }

            var taProcessed = new List<BinanceKlineInsights>();

            var taLibManager = new TaLibManager();

            foreach (var kline in marketData)
            {
                if (taProcessed.Count > 20)
                {
                    var ma20 = taLibManager.Ma(taProcessed.Select(m => Convert.ToDouble(m.Kline.ClosePrice)), TALib.Core.MAType.Sma, 20);
                    kline.MASpike = new ChangeModel { Value = (kline.Kline.HighPrice - Convert.ToDecimal(ma20.Current)) / Convert.ToDecimal(ma20.Current), IsPositive = true };
                    kline.MADrop = new ChangeModel { Value = (Convert.ToDecimal(ma20.Current) - kline.Kline.LowPrice) / kline.Kline.LowPrice, IsPositive = false };
                }

                taProcessed.Add(kline);
            }

            var orderedByMaPerformance = taProcessed.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs).ToList();
            var topMaSpike = orderedByMaPerformance.First();
            var top1percentMaAvg = orderedByMaPerformance.Take((int)(marketData.Count * 0.01)).Average(m => m.MAChange.Abs);
            var top5percentMaAvg = orderedByMaPerformance.Take((int)(marketData.Count * 0.05)).Average(m => m.MAChange.Abs);
            var top10percentMaAvg = orderedByMaPerformance.Take((int)(marketData.Count * 0.1)).Average(m => m.MAChange.Abs);

            foreach (var kline in orderedByMaPerformance.Take((int)(marketData.Count * 0.01)))
            {
                var subsequentKLines = marketData.SkipWhile(m => kline.Kline.OpenTime != m.Kline.OpenTime).Skip(1).ToList();

                int candlesTillProfit = 0;
                int candlesTillStopLoss = 0;

                foreach (var marketKline in subsequentKLines)
                {
                    candlesTillProfit++;
                    candlesTillStopLoss++;

                    if (kline.MAChange.IsPositive)
                    {
                        if (marketKline.Kline.LowPrice < (kline.Kline.HighPrice - (kline.Kline.HighPrice * 0.01m)))
                        {
                            kline.CandlesTillProfit = candlesTillProfit;
                            break;
                        }

                        if (marketKline.Kline.HighPrice > (kline.Kline.HighPrice + (kline.Kline.HighPrice * 0.03m)))
                        {
                            kline.CandlesTillStopLoss = candlesTillStopLoss;
                            break;
                        }
                    }
                    else
                    {
                        if (marketKline.Kline.HighPrice > (kline.Kline.LowPrice + (kline.Kline.LowPrice * 0.01m)))
                        {
                            kline.CandlesTillProfit = candlesTillProfit;
                            break;
                        }

                        if (marketKline.Kline.LowPrice < (kline.Kline.LowPrice - (kline.Kline.LowPrice * 0.03m)))
                        {
                            kline.CandlesTillStopLoss = candlesTillProfit;
                            break;
                        }
                    }
                }
            }

            var profitability = (double)orderedByMaPerformance.Take((int)(marketData.Count * 0.01)).Where(m => m.CandlesTillStopLoss == null).Count() / (double)orderedByMaPerformance.Take((int)(marketData.Count * 0.01)).Count();
            Console.WriteLine($"{symbol}: {profitability.ToString("P2")} profitability, average in  {orderedByMaPerformance.Take((int)(marketData.Count * 0.01)).Where(m => m.CandlesTillProfit.HasValue).Average(m => m.CandlesTillProfit.Value)} candles");
        }

        private static void RunTelegramBot()
        {
            bot = new Telegram.Telebot("6175182837:AAHPvR7-X9ldM7KGVN6l88z-G3k7wrFrhNs");
            //bot.SendFeedbackHandler += Bot_SendFeedbackHandler;
            //bot.AskForCardHandler += Bot_AskForCardHandler;
        }

        private static void HandleSymbolUpdate(DataEvent<IBinanceStreamKlineData> data)
        {
            if (data.Data.Symbol == "ETHUSDT")
            {
                var closePrice = data.Data.Data.ClosePrice;
                binanceClientService.PlaceOrder(data.Data.Symbol, closePrice, OrderSide.Sell);

                return;
            }
            else
            {
                return;
            }

            if (!tradingState.State.ContainsKey(data.Data.Symbol))
            {
                tradingState.State[data.Data.Symbol] = new SymbolTradeState();

                if (tradingState.CanTrade())
                {
                    //binanceClientService.PlaceOrder(data.Data.Symbol, data.Data.Data.ClosePrice);
                }
            }
            else
            {
                if (DateTime.Now > tradingState.State[data.Data.Symbol].LastUpdatedOn.AddHours(1))
                {
                    tradingState.State[data.Data.Symbol].LastUpdatedOn = DateTime.Now;
                    // TODO re-create order!

                }
            }



        }
    }
}


