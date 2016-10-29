#load "Utils.csx"

using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using System.Diagnostics;
using Autofac;
using System.Text;
using Microsoft.Rest;

[Serializable]
public class EchoDialog : IDialog<object>
{
    protected int count = 1;

    public Task StartAsync(IDialogContext context)
    {
        try
        {
            context.Wait(MessageReceivedAsync);
        }
        catch (OperationCanceledException error)
        {
            return Task.FromCanceled(error.CancellationToken);
        }
        catch (Exception error)
        {
            return Task.FromException(error);
        }

        return Task.CompletedTask;
    }

    public virtual async Task MessageReceivedAsync(IDialogContext dlgCtxt, IAwaitable<IMessageActivity> argument)
    {
        var message = await argument;

        var connector = new ConnectorClient(new Uri("https://intercomScratch.azure-api.net"), new Microsoft.Bot.Connector.MicrosoftAppCredentials());
        var sc = new StateClient(new Uri(message.ChannelId == "emulator" ? message.ServiceUrl : "https://intercom-api-scratch.azurewebsites.net"), new MicrosoftAppCredentials());

        if (message.Text.Contains("MessageTypesTest"))
        {
            Trace.TraceInformation("Starting MessageTypesTest");
            var mtResult = await messageTypesTest((Activity) message, connector, sc); 
            await connector.Conversations.ReplyToActivityAsync(mtResult);
        }
        else if (message.Text.Contains("DataTypesTest"))
        {
            var dtResult = await dataTypesTest((Activity) message, connector, sc);
            await connector.Conversations.ReplyToActivityAsync(dtResult);
        }
        else if (message.Text.Contains("CardTypesTest"))
        {
            var ctResult = await cardTypesTest((Activity) message, connector);
            await connector.Conversations.ReplyToActivityAsync(ctResult);
        }
        else
        {
            await dlgCtxt.PostAsync(translateToPigLatin(message.Text));
        }
        dlgCtxt.Wait(MessageReceivedAsync);
    }

    private async Task<Activity> messageTypesTest(Activity message, ConnectorClient connector, StateClient sc)
    {
        StringBuilder sb = new StringBuilder();
        var botState = new BotState(sc);
        // DM a user 
        var newDirectToUser = new Activity()
        {
            Text = "Should go directly to user",
            Type = "message",
            From = message.Recipient,
            Recipient = message.From,
            ChannelId = message.ChannelId
        };

        var ConversationId = await connector.Conversations.CreateDirectConversationAsync(message.Recipient, message.From);
        newDirectToUser.Conversation = new ConversationAccount(id: ConversationId.Id);
        var reply = await connector.Conversations.SendToConversationAsync(newDirectToUser);
        if (reply != null)
            sb.AppendLine(reply.Message);


        // message to conversation not directed to user using CreateReply
        Activity replyToConversation = message.CreateReply("Should go to conversation, but does not address the user that generated it");
        var bcReply = await connector.Conversations.SendToConversationAsync(replyToConversation);
        if(bcReply != null)
            sb.AppendLine(bcReply.Message);

        // reply to to user using CreateReply
        Activity replyToConversation = message.CreateReply("Should go to conversation, but addressing the user that generated it");
        replyToConversation.Recipient = message.From;
        var reply = await connector.Conversations.SendToConversationAsync(replyToConversation);
        if (reply != null)
            sb.AppendLine(reply.Message);

        return message.CreateReply(translateToPigLatin("Completed MessageTypesTest"));
    }

    private async Task<Activity> cardTypesTest(Activity message, ConnectorClient connector)
    {



        return message.CreateReply(translateToPigLatin("Completed CardTypesTest"));
    }

    private async Task<Activity> dataTypesTest(Activity message, ConnectorClient connector, StateClient sc)
    {

        BotState botState = new BotState(sc);
        StringBuilder sb = new StringBuilder();
        // DM a user 
        DateTime timestamp = DateTime.UtcNow;

        pigLatinBotUserData addedUserData = new pigLatinBotUserData();
        BotData botData = new BotData();
        try
        {
            botData = (BotData)await botState.GetUserDataAsync(message.ChannelId, message.From.Id);
        }
        catch (Exception e)
        {
            if (e.Message == "Resource not found")
            { }
            else
                throw e;
        }

        if (botData == null)
            botData = new BotData(eTag: "*");

        addedUserData = botData.GetProperty<pigLatinBotUserData>("v1") ?? new pigLatinBotUserData();

        addedUserData.isNewUser = false;
        addedUserData.lastReadLegalese = timestamp;

        try
        {
            botData.SetProperty("v1", addedUserData);
            var response = await botState.SetUserDataAsync(message.ChannelId, message.From.Id, botData);
        }
        catch (Exception e)
        {
            Trace.WriteLine(e.Message);
        }

        try
        {
            botData = (BotData)await botState.GetUserDataAsync(message.ChannelId, message.From.Id);
        }
        catch (Exception e)
        {
            if (e.Message == "Resource not found")
            { }
            else
                throw e;
        }

        if (botData == null)
            botData = new BotData(eTag: "*");

        addedUserData = botData.GetProperty<pigLatinBotUserData>("v1") ?? new pigLatinBotUserData();

        if (addedUserData.isNewUser != false || addedUserData.lastReadLegalese != timestamp)
            sb.Append(translateToPigLatin("Bot data didn't match doofus."));
        else
            sb.Append(translateToPigLatin("Yo, that get/save/get thing worked."));

        return message.CreateReply(sb.ToString(),"en-Us");

    }
}