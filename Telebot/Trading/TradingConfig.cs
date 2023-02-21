namespace Telebot.Trading
{
    internal class TradingConfig
    {
        public decimal TradedPercentage { get; set; } = 1;
        public int Leverage { get; set; } = 20;
        public int SymbolsInTrade { get; set; } = 5;
        public double TakeProfitPercentage = 0.01;
        public double StopLossPercentage = 0.03;
        public double SpikeDetectionTopPercentage => 0.01;
        public int InsightsDataRecordsAmount => 20000;   
    }
}

