namespace Telebot.Telegram
{
    internal interface ITelebotUsersStore
    {
        void StoreUser(long id);
        List<long> GetAllUsers();
        void RemoveUser(long id);
    }
}