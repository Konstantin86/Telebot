using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telebot.Trading
{
    internal class TradingState
    {
        public Dictionary<string, SymbolTradeState> State { get; set; } = new Dictionary<string, SymbolTradeState>();

        internal bool CanTrade()
        {
            // check if there are place for new positions
            throw new NotImplementedException();
        }
    }
}
