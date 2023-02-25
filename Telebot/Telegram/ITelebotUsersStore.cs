namespace Telebot.Telegram
{
    internal interface ITelebotUsersStore
    {
        List<long> Users { get; set; }
    }
}