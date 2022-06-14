using System;

namespace Randstad.GoogleBigQueryConsumer.Template.Application
{
    internal interface IErrorHandler
    {
        void ResetKnownErrorsCount();
        bool Handle(Exception ex, Guid correlationId);
    }
}