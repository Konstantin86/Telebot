using System.Text;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json;
using Telebot.Binance;
using Telebot.Trading;
using Telebot.Trading.TA;
using Telebot.Utilities;

namespace Telebot
{
    internal class Program
    {
        static Telegram.Telebot? telebot = null;
        static BinanceClientService? binanceClientService;
        static TradingState? tradingState;
        static TradingConfig tradingConfig = new TradingConfig();
        static TaLibManager taLibManager = new TaLibManager();

        const string TradingStateFileReference = "tradingState.json";
        const string TradingConfigFileReference = "tradingConfig.json";
        const int BinanceApiDataRecordsMaxCount = 500;

        static Timer setTakeProfitsJob = null;

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            InitTradingConfig();

            RunTelegramBot();
            InitBinanceClient();

            await LoadTradingInsights();
            InitBinanceClient();
            await RunTradingBot();

            RunBackgroundJobs();

            Console.ReadLine();

            System.IO.File.WriteAllText(TradingStateFileReference, JsonConvert.SerializeObject(tradingState));
            System.IO.File.WriteAllText(TradingConfigFileReference, JsonConvert.SerializeObject(tradingConfig));
            // todo add timer job to close orphant TP / SL orders once a minutes
        }

        private static void InitTradingConfig()
        {
            tradingConfig = System.IO.File.Exists(TradingConfigFileReference) ? JsonConvert.DeserializeObject<TradingConfig>(System.IO.File.ReadAllText(TradingConfigFileReference)) ?? new TradingConfig() : new TradingConfig();
        }

        private static void RunBackgroundJobs()
        {
            setTakeProfitsJob = new Timer(SetTakeProfitsJobHandler, null, TimeSpan.Zero, TimeSpan.FromMinutes(15));
        }

        private static async void SetTakeProfitsJobHandler(object? state)
        {
            if (binanceClientService != null && tradingConfig.AutosetTakeProfit)
            {
                var messages = await binanceClientService.SetTakeProfitsWhereMissing();

                foreach (var msg in messages)
                {
                    await telebot.SendUpdate(msg);
                }
            }
        }

        static async void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            var msg = "Bot has been stopped...";
            Console.WriteLine(msg);

            if (telebot != null)
            {
                await telebot.SendUpdate(msg);
            }
        }

        static async void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"Exception has occured: {e.ExceptionObject.ToString()}");

            File.WriteAllText("error.log", e.ExceptionObject.ToString());

            if (telebot != null)
            {
                await telebot.SendUpdate($"Exception has occured: {e.ExceptionObject.ToString()}");
            }

            Environment.Exit(1);
        }

        private static async Task LoadTradingInsights()
        {
            var symbols = await binanceClientService.GetFuturesPairs();

            tradingState = System.IO.File.Exists(TradingStateFileReference) ? JsonConvert.DeserializeObject<TradingState>(System.IO.File.ReadAllText(TradingStateFileReference)) ?? new TradingState() : new TradingState();

            int count = 1;

            foreach (var symbol in symbols.Where(m => m.Value < DateTime.UtcNow.AddDays(-10)).Select(m => m.Key))
            {
                Console.WriteLine($"{symbol}: Loading data ({count++} / {symbols.Count()})...");

                var klineInsightsData = await LoadInsightsForSymbol(symbol);

                if (!tradingState.State.ContainsKey(symbol))
                    tradingState.State[symbol] = new SymbolTradeState { KlineInsights = klineInsightsData };

                Console.WriteLine($"{symbol}: (listing date {symbols[symbol].ToShortDateString()})" +
                    $" {tradingState.State[symbol].GetProfitability(tradingConfig.SpikeDetectionTopPercentage).ToString("P2")} profitability, " +
                    $"average in  {tradingState.State[symbol].GetProfitAverageTimeInCandles(tradingConfig.SpikeDetectionTopPercentage)} candles");

                //var slCasesToStudy = tradingState.State[symbol].GetStopLossCases(tradingConfig.SpikeDetectionTopPercentage);
            }

            tradingState.RefreshVolumeProfileData();

            System.IO.File.WriteAllText(TradingStateFileReference, JsonConvert.SerializeObject(tradingState));
        }

        private static void RunTelegramBot() 
        { 
            telebot = new Telegram.Telebot("6175182837:AAHPvR7-X9ldM7KGVN6l88z-G3k7wrFrhNs");
            telebot.SaveHandler += Telebot_SaveHandler;
            telebot.StartHandler += Telebot_StartHandler;
            telebot.TopMovesHandler += Telebot_TopMovesHandler;
            telebot.ConfigHandler += Telebot_ConfigHandler;
            telebot.VolumeProfileHandler += Telebot_VolumeProfileHandler;
        }

        private static async void Telebot_VolumeProfileHandler(long chatId, string[] parameters)
        {
            if (parameters == null || parameters.Length == 0) return;

            var symbol = parameters[0];

            if (symbol == null) return;

            tradingState.State[symbol.ToSymbol()].RefreshPriceBins(parameters.Length > 1 ? int.Parse(parameters[1]) : 90);
            var bins = tradingState.State[symbol.ToSymbol()].PriceBins;

            var sb = new StringBuilder();

            var strongestLevels = FindPeaks(bins)
                                    .Where(m => m.Significance > 0.75m)
                                    .OrderByDescending(m => m.Significance)
                                    .Select(m => m.Price)
                                    .ToList();

            double currentPrice = tradingState.State[symbol.ToSymbol()].KlineInsights.Last().ClosePrice;
            var closestPriceBin = currentPrice.FindClosestValue(bins.Select(m => m.Price).ToList());
            var rounder = bins.Last().Price.GetThreeDigitsRounder();

            foreach (var bin in bins.OrderByDescending(b => b.Price))
            {
                bool isMajorLevel = strongestLevels.Contains(bin.Price);
                bool isClosesPriceBin = bin.Price == closestPriceBin;
                
                double roundedPrice = (Math.Round(((bin.Price + (bin.Price + tradingState.State[symbol.ToSymbol()].BinSize)) / 2) / rounder) * rounder);

                var vpInfoLine = $"${roundedPrice.ToString("G5")}: " +
                    //$"({bin.Volume.PercentileOf(bins.Select(m => m.Volume).ToArray()).ToString("P0")})" +
                    $"{new string('-', Convert.ToInt32(Math.Ceiling((bin.Volume.PercentileOf(bins.Select(m => m.Volume).ToArray()) * 100) / 5)))}";

                sb.AppendLine($"{(isMajorLevel ? $"<b>{vpInfoLine}> ({strongestLevels.IndexOf(bin.Price) + 1})</b>" : vpInfoLine)}{(isClosesPriceBin ? $" >> ${currentPrice.ToString("G5")}" : "")}");
            }

            await telebot.SendUpdate(sb.ToString(), chatId);
        }

        public static List<PriceBin> FindPeaks(List<PriceBin> values)
        {
            var peaks = new List<PriceBin>();
            var prevValue = decimal.MinValue;
            var currentValue = decimal.MinValue;
            var volumeValues = values.Select(m => m.Volume).ToArray();

            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    prevValue = volumeValues[i - 1];

                currentValue = volumeValues[i];

                if (i < values.Count - 1)
                    if (currentValue > prevValue && currentValue > volumeValues[i + 1])   // check if current value is greater than both its neighbors
                        peaks.Add(values[i]);
            }

            return peaks;
        }

        private static async void Telebot_ConfigHandler(long chatId, string[] parameters)
        {
            if (parameters == null || parameters.Length < 2)
            {
                var configMessage = JsonConvert.SerializeObject(tradingConfig, Formatting.Indented);
                await telebot.SendUpdate(configMessage, chatId);
            }
            else
            {
                var propertyInfo = tradingConfig.GetType().GetProperty(parameters[0]);
                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    propertyInfo.SetValue(tradingConfig, Convert.ChangeType(parameters[1], propertyInfo.PropertyType));
                    await telebot.SendUpdate($"Parameter {parameters[0]} has been set to {parameters[1]}");
                    File.WriteAllText(TradingConfigFileReference, JsonConvert.SerializeObject(tradingConfig));
                }
                else
                {
                    await telebot.SendUpdate($"Parameter {parameters[0]} cannot be found");
                }
            }
        }

        private static async void Telebot_TopMovesHandler(long chatId, string symbol)
        {
            var topMovesMessages = tradingState.State
                            .Where(m => string.IsNullOrEmpty(symbol) ? true : m.Key == symbol)
                            .Select(m => new { m.Key, MaChangeHistoricalPercentage = m.Value.GetCurrentMaChangeHistoricalPercentage(), m.Value.KlineInsights.Last().ClosePrice, m.Value.KlineInsights.Last().MAChange })
                            .OrderByDescending(m => m.MaChangeHistoricalPercentage)
                            .Take(20)
                            .Select(m => $"{m.Key.ToChartHyperLink()} ({m.ClosePrice.ToString("C4")}): {(m.MAChange.IsPositive ? "+" : "-")}{m.MAChange.Abs.ToString("P1")} {(m.MAChange.IsPositive ? "📈" : "📉")} (top {(1 - m.MaChangeHistoricalPercentage).ToString("P2")})")
                            .ToList();

            string topMovesMessage = string.Join("\n", topMovesMessages.Select((str, i) => $"{i + 1}. {str}"));

            await telebot.SendUpdate($"Current top 20 moves from MA20 on the 1h timeframe:\n{topMovesMessage}", chatId);
        }

        private static void Telebot_StartHandler(long chatId)
        {
            if (tradingState == null) return;

            var informationalSignalsForRecent24h = tradingState.State.Where(m => m.Value.LastInformDate > DateTime.Now.AddHours(-24)).ToList();
            var ordersSignalsForRecent24h = tradingState.State.Where(m => m.Value.LastOrderDate > DateTime.Now.AddHours(-24)).ToList();

            var sb = new StringBuilder();

            if (informationalSignalsForRecent24h.Count > 0)
            {
                sb.AppendLine($"Information signals for the recent 24h:");

                foreach (var informationSingal in informationalSignalsForRecent24h)
                {
                    sb.AppendLine($"{informationSingal.Key.ToChartHyperLink()}: {informationSingal.Value.LastInformDate.ToString("g")}");
                }
            }

            if (ordersSignalsForRecent24h.Count > 0)
            {
                sb.AppendLine($"Orders for the recent 24h:");

                foreach (var order in ordersSignalsForRecent24h)
                {
                    sb.AppendLine($"<a href=\"{order.Key.ToBinanceSymbolChartLink()}\">{order.Key}</a>: {order.Value.LastOrderDate.ToString("g")}");
                }
            }

            if (sb.Length > 0)
            {
                telebot?.SendUpdate(sb.ToString(), chatId);
            }
        }

        private static void Telebot_SaveHandler(long chatId)
        {
            System.IO.File.WriteAllText(TradingStateFileReference, JsonConvert.SerializeObject(tradingState));
            telebot?.SendUpdate("State was successfully saved", chatId);
        }

        private static async Task RunTradingBot() => await binanceClientService.OpenFuturesStream(HandleSymbolUpdate);
        private static void InitBinanceClient() => binanceClientService = new BinanceClientService("gvNqiHE4DJKhSREACPghpwSb9zrXaObCIriMJAZN1J0ptfLY8cLexZpqkXJGqD0s", "S1mMv1ZXUOWyWz6eEJCtZe23Pxvyx7As51EfVniJtmKXGQTClD7jxnHvs0W6XXnK", tradingConfig, tradingState);

        private static async Task<List<BinanceKlineInsights>> LoadInsightsForSymbol(string symbol)
        {
            var marketData = new List<BinanceKlineInsights>();

            bool dataPreloaded = tradingState.State.ContainsKey(symbol);

            var hoursToLoad = !dataPreloaded ? tradingConfig.InsightsDataRecordsAmount : (int)(DateTime.UtcNow - tradingState.State[symbol].KlineInsights.Last().OpenTime).TotalHours;

            int loadingPagesCount = (int)Math.Round((double)hoursToLoad / BinanceApiDataRecordsMaxCount, MidpointRounding.ToPositiveInfinity);

            while (loadingPagesCount > 0)
            {
                int hoursCount = Math.Min(hoursToLoad, BinanceApiDataRecordsMaxCount);

                var start = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, Math.Min(hoursCount * loadingPagesCount, hoursToLoad));
                var end = DateTime.UtcNow.RewindPeriodsBack(KlineInterval.OneHour, Math.Min(hoursCount * loadingPagesCount, hoursToLoad)).AddHours(hoursCount);

                var result = await binanceClientService.GetMarketData(symbol, KlineInterval.OneHour, start, end);
                marketData.AddRange(result.Data.Where(m => m.CloseTime < DateTime.UtcNow).Select(m => new BinanceKlineInsights(m)));

                loadingPagesCount--;

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

            var topPercentage = orderedByMaPerformance.Take((int)((dataPreloaded ? tradingState.State[symbol].KlineInsights : marketData).Count * tradingConfig.SpikeDetectionTopPercentage)).ToList();

            foreach (var kline in topPercentage)
            {
                var subsequentKLines = (dataPreloaded ? tradingState.State[symbol].KlineInsights : marketData).SkipWhile(m => kline.OpenTime != m.OpenTime).Skip(1).ToList();

                int candlesCount = 0;

                foreach (var marketKline in subsequentKLines)
                {
                    candlesCount++;

                    if (kline.MAChange.IsPositive)
                    {
                        if (marketKline.LowPrice < (kline.HighPrice - (kline.HighPrice * tradingConfig.TakeProfitPercentage)))
                        {
                            kline.CandlesTillProfit = candlesCount;
                            break;
                        }

                        if (marketKline.HighPrice > (kline.HighPrice + (kline.HighPrice * tradingConfig.StopLossPercentage)))
                        {
                            kline.CandlesTillStopLoss = candlesCount;
                            break;
                        }
                    }
                    else
                    {
                        if (marketKline.HighPrice > (kline.LowPrice + (kline.LowPrice * tradingConfig.TakeProfitPercentage)))
                        {
                            kline.CandlesTillProfit = candlesCount;
                            break;
                        }

                        if (marketKline.LowPrice < (kline.LowPrice - (kline.LowPrice * tradingConfig.StopLossPercentage)))
                        {
                            kline.CandlesTillStopLoss = candlesCount;
                            break;
                        }
                    }
                }
            }

            return taProcessed;
        }

        private static async void HandleSymbolUpdate(DataEvent<IBinanceStreamKlineData> data)
        {
            var symbol = data.Data.Symbol;
            var closePrice = Convert.ToDouble(data.Data.Data.ClosePrice);

            if (!tradingState.State.ContainsKey(symbol))
            {
                //telebot.SendUpdate($"New futures trading pair {symbol} detected. Please restart the bot to load insights for it in 5 days from now in order to trade this symbol");
                return;    // New symbols needs app reloading to work properly, todo add some telegram / console message here...
            }

            var symbolsToMonitor = new []{ "BTCUSDT" }.Union(tradingState.State.Where(m => m.Value.IsMarkedOpen()).Select(m => m.Key)).ToList();

            if (data.Data.Data.Final)
            {
                var klineInsights = new BinanceKlineInsights(data.Data.Data);
                tradingState.State[symbol].KlineInsights.Add(klineInsights);

                klineInsights.MA20 = taLibManager.Ma(tradingState.State[symbol].KlineInsights.Select(m => m.ClosePrice), TALib.Core.MAType.Sma, 20).Current;
                klineInsights.MAChange = (klineInsights.HighPrice - klineInsights.MA20) > 0
                    ? new ChangeModel { Value = (klineInsights.HighPrice - klineInsights.MA20) / klineInsights.MA20, IsPositive = true }
                    : new ChangeModel { Value = (klineInsights.MA20 - klineInsights.LowPrice) / klineInsights.LowPrice, IsPositive = false };

                tradingState.State[symbol].RefreshPriceBins();
            }

            var lastInsightsRecord = tradingState.State[symbol].KlineInsights.Last();
            var ma20 = lastInsightsRecord.MA20;
            lastInsightsRecord.MAChange = (closePrice - ma20) > 0
                ? new ChangeModel { Value = (closePrice - ma20) / ma20, IsPositive = true }
                : new ChangeModel { Value = (ma20 - closePrice) / closePrice, IsPositive = false };

            lastInsightsRecord.ClosePrice = closePrice;

            if (tradingState.State[symbol].EnterSpikeDetected(lastInsightsRecord.MAChange, tradingConfig.SpikeDetectionTopPercentage))
            {
                try
                {
                    var placingOrderResult = await binanceClientService.PlaceOrder(data.Data.Symbol, data.Data.Data.ClosePrice, lastInsightsRecord.MAChange.IsPositive ? OrderSide.Sell : OrderSide.Buy);

                    if (placingOrderResult != null)
                    {
                        await telebot.SendUpdate(placingOrderResult);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    await telebot.SendUpdate(ex.ToString());
                }
            }
            else if (tradingState.State[symbol].InformSpikeDetected(lastInsightsRecord.MAChange, tradingConfig.SpikeDetectionTopPercentage))
            {
                if ((DateTime.Now - tradingState.State[symbol].LastInformDate).TotalHours > 1)
                {
                    tradingState.State[symbol].LastInformDate = DateTime.Now;
                    await telebot.SendUpdate($"{symbol.ToChartHyperLink()}: Price is {closePrice.ToString("C4")} which is {lastInsightsRecord.MAChange.Abs.ToString("P2")} {(lastInsightsRecord.MAChange.IsPositive ? "growth 📈" : "drop 📉")} from MA20 which is in historical top 1% range. Monitor for entry area here...");
                }
            }

            // Check existing open positions without take profit and set take profits automatically (make it with a separate timer job, once a ten minutes)
        }
    }
}