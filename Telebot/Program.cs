using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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

            if (File.Exists($"tradingState.json"))
            {
                var fileStr = File.ReadAllText($"tradingState.json");
                tradingState = JsonConvert.DeserializeObject<TradingState>(fileStr);
            }

            foreach (var symbol in symbols.Select(m => m.Key).Take(4))
            {
                var klineInsightsData = RunBackTest(symbol);

                if (tradingState.State.ContainsKey(symbol))
                {
                    tradingState.State[symbol].KlineInsights.AddRange(klineInsightsData);
                }
                else
                {
                    tradingState.State[symbol] = new SymbolTradeState { KlineInsights = klineInsightsData };
                }

                var orderedByMaPerformance = tradingState.State[symbol].KlineInsights.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs).ToList();

                var profitability = (double)orderedByMaPerformance.Take((int)(orderedByMaPerformance.Count * 0.01)).Where(m => m.CandlesTillStopLoss == null).Count() / (double)orderedByMaPerformance.Take((int)(orderedByMaPerformance.Count * 0.01)).Count();
                Console.WriteLine($"{symbol}: {profitability.ToString("P2")} profitability, average in  {orderedByMaPerformance.Take((int)(orderedByMaPerformance.Count * 0.01)).Where(m => m.CandlesTillProfit.HasValue).Average(m => m.CandlesTillProfit.Value)} candles");
            }

            var str = JsonConvert.SerializeObject(tradingState);
            File.WriteAllText($"tradingState.json", str);

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
            //binanceClientService.OpenFuturesStream(HandleSymbolUpdate);
        }

        private static List<BinanceKlineInsights> RunBackTest(string symbol)
        {
            var marketData = new List<BinanceKlineInsights>();

            var hoursToLoad = !tradingState.State.ContainsKey(symbol) ? 20000 : (int)(DateTime.UtcNow - tradingState.State[symbol].KlineInsights.Last().OpenTime).TotalHours;

            int i = (int)Math.Round((double)hoursToLoad / 500, MidpointRounding.ToPositiveInfinity);

            while (i > 0)
            {
                int hoursCount = Math.Min(hoursToLoad, 500);

                var start = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, Math.Min(hoursCount * i, hoursToLoad));
                var end = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, Math.Min(hoursCount * i, hoursToLoad)).AddHours(hoursCount);

                var result = binanceClientService.GetMarketData(symbol, KlineInterval.OneHour, start, end);
                marketData.AddRange(result.Data.Select(m => new BinanceKlineInsights(m)));

                i--;

                hoursToLoad = hoursToLoad - hoursCount;
            }

            var taProcessed = new List<BinanceKlineInsights>();

            if (marketData.Count > 0)
            {

                var taLibManager = new TaLibManager();

                foreach (var kline in marketData)
                {
                    if (taProcessed.Count > 20)
                    {
                        var ma20 = taLibManager.Ma(taProcessed.Select(m => m.ClosePrice), TALib.Core.MAType.Sma, 20);
                        kline.MAChange = (kline.HighPrice - ma20.Current) > 0
                            ? new ChangeModel { Value = (kline.HighPrice - ma20.Current) / ma20.Current, IsPositive = true }
                            : new ChangeModel { Value = (ma20.Current - kline.LowPrice) / kline.LowPrice, IsPositive = false };
                    }

                    taProcessed.Add(kline);
                }

                var orderedByMaPerformance = taProcessed.Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs).ToList();
                //var topMaSpike = orderedByMaPerformance.First();
                //var top1percentMaAvg = orderedByMaPerformance.Take((int)(marketData.Count * 0.01)).Average(m => m.MAChange.Abs);
                //var top5percentMaAvg = orderedByMaPerformance.Take((int)(marketData.Count * 0.05)).Average(m => m.MAChange.Abs);
                //var top10percentMaAvg = orderedByMaPerformance.Take((int)(marketData.Count * 0.1)).Average(m => m.MAChange.Abs);

                foreach (var kline in orderedByMaPerformance.Take((int)(marketData.Count * 0.01)))
                {
                    var subsequentKLines = marketData.SkipWhile(m => kline.OpenTime != m.OpenTime).Skip(1).ToList();

                    int candlesTillProfit = 0;
                    int candlesTillStopLoss = 0;

                    foreach (var marketKline in subsequentKLines)
                    {
                        candlesTillProfit++;
                        candlesTillStopLoss++;

                        if (kline.MAChange.IsPositive)
                        {
                            if (marketKline.LowPrice < (kline.HighPrice - (kline.HighPrice * 0.01)))
                            {
                                kline.CandlesTillProfit = candlesTillProfit;
                                break;
                            }

                            if (marketKline.HighPrice > (kline.HighPrice + (kline.HighPrice * 0.03)))
                            {
                                kline.CandlesTillStopLoss = candlesTillStopLoss;
                                break;
                            }
                        }
                        else
                        {
                            if (marketKline.HighPrice > (kline.LowPrice + (kline.LowPrice * 0.01)))
                            {
                                kline.CandlesTillProfit = candlesTillProfit;
                                break;
                            }

                            if (marketKline.LowPrice < (kline.LowPrice - (kline.LowPrice * 0.03)))
                            {
                                kline.CandlesTillStopLoss = candlesTillProfit;
                                break;
                            }
                        }
                    }
                }
            }

            return taProcessed;
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
                binanceClientService.PlaceOrder(data.Data.Symbol, closePrice, OrderSide.Buy);

                return;
            }
            else
            {
                return;
            }

            //if (!tradingState.State.ContainsKey(data.Data.Symbol))
            //{
            //    tradingState.State[data.Data.Symbol] = new SymbolTradeState();

            //    if (tradingState.CanTrade())
            //    {
            //        //binanceClientService.PlaceOrder(data.Data.Symbol, data.Data.Data.ClosePrice);
            //    }
            //}
            //else
            //{
            //    if (DateTime.Now > tradingState.State[data.Data.Symbol].LastUpdatedOn.AddHours(1))
            //    {
            //        tradingState.State[data.Data.Symbol].LastUpdatedOn = DateTime.Now;
            //        // TODO re-create order!

            //    }
            //}



        }
    }
}


