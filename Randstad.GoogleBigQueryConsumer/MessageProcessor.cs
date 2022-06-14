using Google.Apis.Auth.OAuth2;
using Google.Apis.Bigquery.v2.Data;
using Google.Cloud.BigQuery.V2;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Randstad.GoogleBigQueryConsumer.Template.Application;
using Randstad.Logging;
using RandstadMessageExchange;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Randstad.GoogleBigQueryConsumer
{
    internal class MessageProcessor : IMessageProcessor
    {
        private readonly ILogger _logger;
        public static BigQueryClient _bigQueryClient;        
        string projectNameId = "uk-sandbox-dev-9b49";
        const string keyFile = "./KeyFile.json";
        string datasetNameId = "sandbox";

        public MessageProcessor(ILogger logger)
        {
            _logger = logger;
            
        }

        /// <summary>
        /// Process the next message and return the action to be taken.
        /// </summary>
        /// <param name="queueMessage">The message consumed from the RabbitMQ queue. It will never be null.</param>
        /// <returns>A <see cref="QueueMessageAction"/> which is the action the <see cref="MessageConsumer"/> should perform on the message.</returns>
        public async Task<QueueMessageAction> Process(QueueMessage queueMessage)
        {
            // This is here to prevent a compiler warning. To make async calls in
            // this method then remove this as you'll be awaiting something else.
            await Task.CompletedTask;

            ///////////////////////////////////////////////////////////////////////////
            // TODO: R E P L A C E   C O D E   B E L O W   W I T H   Y O U R   C O D E
            ///////////////////////////////////////////////////////////////////////////
            string RabbitMQ_RoutingKey = queueMessage.RoutingKey;
            ListTables(RabbitMQ_RoutingKey, queueMessage.Body);
            _logger.Info($"Body: {queueMessage.Body}, QueueCount:{queueMessage.QueueCount}", queueMessage.CorrelationId, null, null, null, null);
            return QueueMessageAction.Acknowledge;
        }

        public void ListTables(string RabbitMQ_RoutingKey, string jsonFileBody)
        {
            
            string[] routingKeySplit = RabbitMQ_RoutingKey.Split(@".");
            string tableName = routingKeySplit[4];


            //BigQueryClient client = GetBigQueryClient();
            //var jsonStream = new FileStream(keyFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            //using (jsonStream)
            //{
            //    string json = File.ReadAllText(jsonFile);
            //    using JsonDocument doc = JsonDocument.Parse(json);
            //    JsonElement root = doc.RootElement;

            //}
            foreach (var item in jsonFileBody)
            {
                item.ToString();
            }

            List<BigQueryTable> tables = _bigQueryClient.ListTables(datasetNameId).ToList();

            var identifyTable = tables.Where(c => c.FullyQualifiedId.Contains(tableName));

            if (identifyTable.Any())
            {

                TableInsertRows(jsonFileBody);
            }
            else
            {
                CreateTable(tableName);
            }
        }
        public void CreateTable(string tableName)
        {
            TableSchemaBuilder schema = new TableSchemaBuilder();
            //string json = File.ReadAllText(jsonFile);
            var fieldName = JObject.Parse(tableName);


            foreach (var item in fieldName)
            {
                schema.Add(item.Key, BigQueryDbType.String);
            }
            Table tableToCreate = new Table();
            tableToCreate.TimePartitioning = TimePartition.CreateDailyPartitioning(expiration: null);

            tableToCreate.Schema = schema.Build();
            BigQueryClient client = BigQueryClient.Create(projectNameId);

            BigQueryTable table = client.CreateTable(datasetNameId, tableName, tableToCreate);
        }
        public List<TableRow> GetRows(string query)
        {
            var bqClient = GetBigQueryClient();

            var response = new List<TableRow>();

            var jobResource = bqClient.Service.Jobs;
            var qr = new QueryRequest() { Query = query };

            var queryResponse = jobResource.Query(qr, projectNameId).Execute();

            if (queryResponse.JobComplete != false)
            {
                return queryResponse.Rows == null
                    ? new List<TableRow>()
                    : queryResponse.Rows.ToList();
            }

            var jobId = queryResponse.JobReference.JobId;

            var retry = true;
            var retryCounter = 0;
            while (retry && retryCounter < 50)
            {
                Thread.Sleep(1000);

                var queryResults = bqClient.Service.Jobs.GetQueryResults(projectNameId, jobId).Execute();

                if (queryResults.JobComplete != true)
                {
                    retryCounter++;
                    continue;
                }

                if (queryResults.Rows != null)
                    response = queryResults.Rows.ToList();

                retry = false;
            }

            return response;
        }
        public void TableInsertRows(object doc)
        {
            var client = GetBigQueryClient();
            Dictionary<string, object> dataRow = new Dictionary<string, object>();
            dataRow.Add(string.Empty, doc);
            BigQueryInsertRow[] rows = new BigQueryInsertRow[]
            {

            // The insert ID is optional, but can avoid duplicate data
            // when retrying inserts.
            new BigQueryInsertRow() {
                dataRow
            }
           };
            client.InsertRows(datasetNameId, "IvanTest", rows);
        }
        public static BigQueryClient GetBigQueryClient()
        {
            var jsonStream = new FileStream(keyFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (jsonStream)
            {
                var credentials = GoogleCredential.FromStream(jsonStream);
                _bigQueryClient = BigQueryClient.Create("uk-sandbox-dev-9b49", credentials);
            }
            return _bigQueryClient;
        }
    }
}
