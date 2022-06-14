using RandstadMessageExchange;
using System.Threading.Tasks;

namespace Randstad.GoogleBigQueryConsumer.Template.Application
{
    internal interface IMessageProcessor
    {
        Task<QueueMessageAction> Process(QueueMessage queueMessage);
    }
}
