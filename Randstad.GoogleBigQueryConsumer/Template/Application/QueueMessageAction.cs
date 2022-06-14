namespace Randstad.GoogleBigQueryConsumer.Template.Application
{
    internal enum QueueMessageAction
    {
        Acknowledge = 0,
        RejectAndRequeue = 1,
        RejectAndDiscard = 2
    }
}
