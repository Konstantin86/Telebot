namespace Telebot.Telegram
{
    public class TelebotUsersStore : ITelebotUsersStore
    {
        public List<long> Users { get; set; } = new List<long>();
    }
}
