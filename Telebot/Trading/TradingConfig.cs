namespace Telebot.Trading
{
    internal class TradingConfig
    {
        public decimal TradedPercentage { get; set; } = 1;
        public int Leverage { get; set; } = 20;
        public int SymbolsInTrade { get; set; } = 10;
        public double TakeProfitPercentage = 0.02;
        public double StopLossPercentage = 0.06;
        public double SpikeDetectionTopPercentage => 0.01;
        public int InsightsDataRecordsAmount => 20000;   
    }
}

