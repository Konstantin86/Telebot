using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Diagnostics;
using Newtonsoft.Json;
using Telebot.Trading;

namespace Telebot.Telegram
{
    internal class Telebot
    {
        private const string TelegramUserStoreFileReference = "userStore.json";
        public TelegramBotClient bot;
        private ITelebotUsersStore usersStore;

        public event Action<long>? StartHandler;
        public event Action<long>? StopHandler;
        public event Action<long>? SaveHandler;
        public event Action<long>? InsightsHandler;
        public event Action<long, string[]>? TopMovesHandler;
        public event Action<long, string[]>? VolumeProfileHandler;
        public event Action<long, string?>? SendFeedbackHandler;
        public event Action<long, string[]>? ConfigHandler;

        public event Action<long, string> GetIdeaLinkHandler;

        public Telebot(string botAccessToken)
        {
            this.bot = new TelegramBotClient(botAccessToken);
            var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions() { AllowedUpdates = { } };

            this.bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

            var me = this.bot.GetMeAsync().Result;
            Console.WriteLine(
              $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
            );
        }

        private void SaveState()
        {
            System.IO.File.WriteAllText(TelegramUserStoreFileReference, JsonConvert.SerializeObject(this.usersStore));
        }

        public async Task InitState()
        {
            this.usersStore = System.IO.File.Exists(TelegramUserStoreFileReference)
                ? JsonConvert.DeserializeObject<TelebotUsersStore>(System.IO.File.ReadAllText(TelegramUserStoreFileReference))
                : new TelebotUsersStore();

            foreach (var userId in this.usersStore.Users)
            {
                await this.bot.SendTextMessageAsync(userId, $"Welcome to telebot. Bot application is up and running ({(int)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalHours} hours, {(DateTime.Now - Process.GetCurrentProcess().StartTime).Minutes} minutes). You're subscribed on updates.");
            }
        }

        public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(update.Message),
                UpdateType.EditedMessage => BotOnMessageReceived(update.Message),
                //UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery),
                //UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery),
                //UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult),
                // UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        public async Task SendUpdate(string message, long? chatId = null)
        {
            if (chatId.HasValue)
            {
                await this.bot.SendTextMessageAsync(chatId.Value, message, ParseMode.Html, disableWebPagePreview: true);
            }
            else
            {
                this.usersStore.Users.ForEach(async m => await this.bot.SendTextMessageAsync(m, message, ParseMode.Html, disableWebPagePreview: true));
            }
        }

        private async Task BotOnMessageReceived(Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text || message.Text == null)
                return;

            string[]? commandParts = message.Text.Split(' ');
            string? command = commandParts.First();
            string? parameter = commandParts.Length > 1 ? commandParts.Last() : null;
            string[]? parameters = commandParts.Length > 1 ? commandParts.Skip(1).ToArray() : null;

            var fromServer = parameter == "fromServer";

            var action = (command) switch
            {
                "/start" => Start(message.Chat.Id),
                "/stop" => Stop(message.Chat.Id),
                "/save" => SaveState(message.Chat.Id),
                "/topmoves" => TopMoves(message.Chat.Id, parameters),
                "/vp" => VolumeProfile(message.Chat.Id, parameters),
                "/insights" => Insights(message.Chat.Id),
                "/config" => Config(message.Chat.Id, parameters),
                "/idea" => GetIdeaLink(message.Chat.Id, parameter),
                _ => Usage(message)
            };
            await action;

            async Task Start(long clientId)
            {
                if (!this.usersStore.Users.Contains(clientId))
                {
                    this.usersStore.Users.Add(clientId);
                }

                this.SaveState();

                await this.bot.SendTextMessageAsync(clientId, $"Welcome to telebot. Bot application is up and running ({(int)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalHours} hours, {(DateTime.Now - Process.GetCurrentProcess().StartTime).Minutes} minutes). You're subscribed on updates.");

                if (StartHandler != null)
                {
                    StartHandler(clientId);
                }
            };

            async Task Stop(long clientId)
            {
                if (this.usersStore.Users.Contains(clientId))
                {
                    this.usersStore.Users.Remove(clientId);
                }

                if (StopHandler != null)
                {
                    StopHandler(clientId);
                }
            };
            
            async Task Insights(long chatId)
            {
                if (InsightsHandler != null)
                {
                    InsightsHandler(chatId);
                }
            };

            async Task SaveState(long chatId)
            {
                if (SaveHandler != null)
                {
                    SaveHandler(chatId);
                }
            };

            async Task VolumeProfile(long clientId, string[] parameters)
            {
                if (VolumeProfileHandler != null)
                {
                    VolumeProfileHandler(clientId, parameters);
                }
            };

            async Task TopMoves(long clientId, string[] parameters)
            {
                if (TopMovesHandler != null)
                {
                    TopMovesHandler(clientId, parameters);
                }
            };

            async Task Config(long clientId, string[] parameters)
            {
                if (ConfigHandler != null)
                {
                    ConfigHandler(clientId, parameters);
                }
            };

            async Task GetIdeaLink(long chatId, string coin)
            {
                if (GetIdeaLinkHandler != null)
                {
                    GetIdeaLinkHandler(chatId, coin);
                }
            };

            async Task Usage(Message message)
            {
                const string? usage = "Usage:\n" +
                                        "/feedback <message> - send feedback\n" +
                                        "/askForCard - request for Kharkiv card creation\n";

                await this.bot.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: usage
                );
            }
        }

            private async Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
        }

        internal void ReplyTo(long clientId, string msg)
        {
            this.bot.SendTextMessageAsync(clientId, msg);
        }
    }
}
