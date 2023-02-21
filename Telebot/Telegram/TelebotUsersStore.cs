namespace Telebot.Telegram
{
    internal class TelebotUsersStore : ITelebotUsersStore
    {
        private List<long> userIds = new List<long>();

        public void StoreUser(long id)
        {
            if (!userIds.Contains(id))
                userIds.Add(id);
        }

        public List<long> GetAllUsers() => userIds;

        public void RemoveUser(long id)
        {
            if (userIds.Contains(id))
                userIds.Remove(id);
        }
    }
}
