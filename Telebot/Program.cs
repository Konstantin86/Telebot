namespace Telebot
{
    internal class Program
    {
        static Telegram.Telebot bot = null;
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            bot = new Telegram.Telebot("6175182837:AAHPvR7-X9ldM7KGVN6l88z-G3k7wrFrhNs");

            bot.SendFeedbackHandler += Bot_SendFeedbackHandler;
            bot.AskForCardHandler += Bot_AskForCardHandler;

            Console.ReadLine();
        }

        private static void Bot_AskForCardHandler(long clientId)
        {
            string replyMsg = $"We've received a request for card from the client: {clientId}";
            bot.ReplyTo(clientId, replyMsg);
        }

        private static void Bot_SendFeedbackHandler(long clientId, string? msg)
        {
            string replyMsg = $"We've received a feedback {msg} from the client: {clientId}";
            bot.ReplyTo(clientId, replyMsg);
        }
    }
}


