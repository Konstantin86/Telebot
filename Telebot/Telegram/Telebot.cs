﻿using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace Telebot.Telegram
{
    internal class Telebot
    {
        private TelegramBotClient bot;

        public event Action<long, string?> SendFeedbackHandler;
        public event Action<long> AskForCardHandler;

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

        private async Task BotOnMessageReceived(Message message, string callbackQueryId = null)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;

            string[] commandParts = message.Text.Split(' ');
            string command = commandParts.First();
            string parameter = commandParts.Last();

            var fromServer = parameter == "fromServer";

            var action = (command) switch
            {
                "/feedback" => SendFeedback(message.Chat.Id, parameter),
                "/askForCard" => AskForCard(message.Chat.Id),
                _ => Usage(message)
            };
            await action;

            async Task SendFeedback(long clientId, string messageText)
            {
                if (SendFeedbackHandler != null)
                {
                    SendFeedbackHandler(clientId, messageText);
                }
            };

            async Task AskForCard(long clientId)
            {
                if (AskForCardHandler != null)
                {
                    AskForCardHandler(clientId);
                }
            };

            async Task Usage(Message message)
            {
                const string usage = "Usage:\n" +
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