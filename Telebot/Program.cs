using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json;
using System.Text;
using Telebot.Binance;
using Telebot.Trading;
using Telebot.Trading.TA;
using Telebot.Utilities;

namespace Telebot
{
    internal class Program
    {
        static Telegram.Telebot telebot = null;
        static BinanceClientService binanceClientService;
        static TradingState tradingState;
        static TradingConfig tradingConfig = new TradingConfig();
        static TaLibManager taLibManager = new TaLibManager();

        const string TradingStateFileReference = "tradingState.json";
        const int BinanceApiDataRecordsMaxCount = 500;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            InitBinanceClient();
            LoadTradingInsights();
            RunTelegramBot();
            InitBinanceClient();
            RunTradingBot();

            Console.ReadLine();

            File.WriteAllText(TradingStateFileReference, JsonConvert.SerializeObject(tradingState));
            // todo add timer job to close orphant TP / SL orders once a minutes
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            var msg = "Bot has been stopped...";
            Console.WriteLine(msg);

            if (telebot != null)
            {
                telebot.SendUpdate(msg);
            }
        }

        static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Exception has occured");
            Console.WriteLine(e.ExceptionObject.ToString());

            if (telebot != null)
            {
                telebot.SendUpdate($"Exception has occured: {e.ExceptionObject.ToString()}");
            }

            Environment.Exit(1);
        }

        private static void LoadTradingInsights()
        {
            var symbols = binanceClientService.GetFuturesPairs();

            tradingState = File.Exists(TradingStateFileReference) ? JsonConvert.DeserializeObject<TradingState>(File.ReadAllText(TradingStateFileReference)) ?? new TradingState() : new TradingState();

            int count = 1;

            foreach (var symbol in symbols.Where(m => m.Value < DateTime.UtcNow.AddDays(-10)).Select(m => m.Key))
            {
                Console.WriteLine($"{symbol}: Loading data ({count++} / {symbols.Count()})...");

                var klineInsightsData = LoadInsightsForSymbol(symbol);

                if (!tradingState.State.ContainsKey(symbol))
                    tradingState.State[symbol] = new SymbolTradeState { KlineInsights = klineInsightsData };

                Console.WriteLine($"{symbol}: (listing date {symbols[symbol].ToShortDateString()})" +
                    $" {tradingState.State[symbol].GetProfitability(tradingConfig.SpikeDetectionTopPercentage).ToString("P2")} profitability, " +
                    $"average in  {tradingState.State[symbol].GetProfitAverageTimeInCandles(tradingConfig.SpikeDetectionTopPercentage)} candles");
            }

            File.WriteAllText(TradingStateFileReference, JsonConvert.SerializeObject(tradingState));
        }

        private static void RunTelegramBot() 
        { 
            telebot = new Telegram.Telebot("6175182837:AAHPvR7-X9ldM7KGVN6l88z-G3k7wrFrhNs");
            telebot.SaveHandler += Telebot_SaveHandler;
        }

        private static void Telebot_SaveHandler(long chatId)
        {
            File.WriteAllText(TradingStateFileReference, JsonConvert.SerializeObject(tradingState));
            telebot.SendUpdate("State was successfully saved", chatId);
        }

        private static void RunTradingBot() => binanceClientService.OpenFuturesStream(HandleSymbolUpdate);
        private static void InitBinanceClient() => binanceClientService = new BinanceClientService("gvNqiHE4DJKhSREACPghpwSb9zrXaObCIriMJAZN1J0ptfLY8cLexZpqkXJGqD0s", "S1mMv1ZXUOWyWz6eEJCtZe23Pxvyx7As51EfVniJtmKXGQTClD7jxnHvs0W6XXnK", tradingConfig, tradingState);

        private static List<BinanceKlineInsights> LoadInsightsForSymbol(string symbol)
        {
            var marketData = new List<BinanceKlineInsights>();

            bool dataPreloaded = tradingState.State.ContainsKey(symbol);

            var hoursToLoad = !dataPreloaded ? tradingConfig.InsightsDataRecordsAmount : (int)(DateTime.UtcNow - tradingState.State[symbol].KlineInsights.Last().OpenTime).TotalHours;

            int i = (int)Math.Round((double)hoursToLoad / BinanceApiDataRecordsMaxCount, MidpointRounding.ToPositiveInfinity);

            while (i > 0)
            {
                int hoursCount = Math.Min(hoursToLoad, BinanceApiDataRecordsMaxCount);

                var start = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, Math.Min(hoursCount * i, hoursToLoad));
                var end = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, Math.Min(hoursCount * i, hoursToLoad)).AddHours(hoursCount);

                var result = binanceClientService.GetMarketData(symbol, KlineInterval.OneHour, start, end);
                marketData.AddRange(result.Data.Where(m => m.CloseTime < DateTime.UtcNow).Select(m => new BinanceKlineInsights(m)));

                i--;

                hoursToLoad = hoursToLoad - hoursCount;
            }

            var taProcessed = new List<BinanceKlineInsights>();

            if (marketData.Count > 0)
            {
                foreach (var kline in marketData)
                {
                    taProcessed.Add(kline);

                    if (dataPreloaded)
                    {
                        tradingState.State[symbol].KlineInsights.Add(kline);

                        kline.MA20 = taLibManager.Ma(tradingState.State[symbol].KlineInsights.Select(m => m.ClosePrice), TALib.Core.MAType.Sma, 20).Current;
                        kline.MAChange = (kline.HighPrice - kline.MA20) > 0
                            ? new ChangeModel { Value = (kline.HighPrice - kline.MA20) / kline.MA20, IsPositive = true }
                            : new ChangeModel { Value = (kline.MA20 - kline.LowPrice) / kline.LowPrice, IsPositive = false };
                    }
                    else
                    {
                        if (taProcessed.Count > 20)
                        {
                            kline.MA20 = taLibManager.Ma(taProcessed.Select(m => m.ClosePrice), TALib.Core.MAType.Sma, 20).Current;
                            kline.MAChange = (kline.HighPrice - kline.MA20) > 0
                                ? new ChangeModel { Value = (kline.HighPrice - kline.MA20) / kline.MA20, IsPositive = true }
                                : new ChangeModel { Value = (kline.MA20 - kline.LowPrice) / kline.LowPrice, IsPositive = false };
                        }
                    }
                }
            }

                var orderedByMaPerformance = (dataPreloaded ? tradingState.State[symbol].KlineInsights : taProcessed).Where(m => m.MAChange != null).OrderByDescending(m => m.MAChange.Abs).ToList();

                foreach (var kline in orderedByMaPerformance.Take((int)((dataPreloaded ? tradingState.State[symbol].KlineInsights : marketData).Count * tradingConfig.SpikeDetectionTopPercentage)))
                {
                    var subsequentKLines = (dataPreloaded ? tradingState.State[symbol].KlineInsights : marketData).SkipWhile(m => kline.OpenTime != m.OpenTime).Skip(1).ToList();

                    int candlesCount = 0;

                    foreach (var marketKline in subsequentKLines)
                    {
                        candlesCount++;

                        if (kline.MAChange.IsPositive)
                        {
                            if (marketKline.HighPrice > (kline.HighPrice + (kline.HighPrice * tradingConfig.StopLossPercentage)))
                            {
                                kline.CandlesTillStopLoss = candlesCount;
                                break;
                            }

                        if (marketKline.LowPrice < (kline.HighPrice - (kline.HighPrice * tradingConfig.TakeProfitPercentage)))
                        {
                            kline.CandlesTillProfit = candlesCount;
                            break;
                        }
                    }
                        else
                        {
                            if (marketKline.LowPrice < (kline.LowPrice - (kline.LowPrice * tradingConfig.StopLossPercentage)))
                            {
                                kline.CandlesTillStopLoss = candlesCount;
                                break;
                            }

                        if (marketKline.HighPrice > (kline.LowPrice + (kline.LowPrice * tradingConfig.TakeProfitPercentage)))
                        {
                            kline.CandlesTillProfit = candlesCount;
                            break;
                        }
                    }
                    }
                }

            return taProcessed;
        }

        private static void HandleSymbolUpdate(DataEvent<IBinanceStreamKlineData> data)
        {
            var symbol = data.Data.Symbol;
            var closePrice = Convert.ToDouble(data.Data.Data.ClosePrice);

            if (!tradingState.State.ContainsKey(symbol))
            {
                //telebot.SendUpdate($"New futures trading pair {symbol} detected. Please restart the bot to load insights for it in 5 days from now in order to trade this symbol");
                return;    // New symbols needs app reloading to work properly, todo add some telegram / console message here...
            }

            if (data.Data.Data.Final)
            {
                var klineInsights = new BinanceKlineInsights(data.Data.Data);
                tradingState.State[symbol].KlineInsights.Add(klineInsights);
                
                klineInsights.MA20 = taLibManager.Ma(tradingState.State[symbol].KlineInsights.Select(m => m.ClosePrice), TALib.Core.MAType.Sma, 20).Current;
                klineInsights.MAChange = (klineInsights.HighPrice - klineInsights.MA20) > 0
                    ? new ChangeModel { Value = (klineInsights.HighPrice - klineInsights.MA20) / klineInsights.MA20, IsPositive = true }
                    : new ChangeModel { Value = (klineInsights.MA20 - klineInsights.LowPrice) / klineInsights.LowPrice, IsPositive = false };
            }
            
            var ma20 = tradingState.State[symbol].KlineInsights.Last().MA20;
            var maChange = (closePrice - ma20) > 0
                ? new ChangeModel { Value = (closePrice - ma20) / ma20, IsPositive = true }
                : new ChangeModel { Value = (ma20 - closePrice) / closePrice, IsPositive = false };

            if (tradingState.State[symbol].SpikeDetected(maChange, tradingConfig.SpikeDetectionTopPercentage))
            {
                try
                {
                    var placingOrderResult = binanceClientService.PlaceOrder(data.Data.Symbol, data.Data.Data.ClosePrice, maChange.IsPositive ? OrderSide.Sell : OrderSide.Buy);

                    if (placingOrderResult != null)
                    {
                        telebot.SendUpdate(placingOrderResult);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    telebot.SendUpdate(ex.ToString());
                }
            }
        }
    }
}