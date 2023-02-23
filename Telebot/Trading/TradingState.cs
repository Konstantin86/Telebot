using Newtonsoft.Json;

namespace Telebot.Trading
{
    internal class TradingState
    {
        public Dictionary<string, SymbolTradeState> State { get; set; } = new Dictionary<string, SymbolTradeState>();

        public void RefreshVolumeProfileData()
        {
            foreach (var pair in State)
            {
                pair.Value.RefreshPriceBins();
            }
        }
    }
}
