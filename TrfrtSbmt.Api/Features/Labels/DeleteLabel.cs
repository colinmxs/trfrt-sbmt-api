﻿namespace TrfrtSbmt.Api.Features.Labels;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Threading;
using System.Threading.Tasks;
using TrfrtSbmt.Api.DataModels;

public class DeleteLabel
{
    public record DeleteFortCommand(string Id) : IRequest;

    public class CommandHandler : AsyncRequestHandler<DeleteFortCommand>
    {
        private readonly IAmazonDynamoDB _db;
        private readonly AppSettings _settings;

        public CommandHandler(IAmazonDynamoDB db, AppSettings settings)
        {
            _db = db;
            _settings = settings;
        }

        protected override async Task Handle(DeleteFortCommand request, CancellationToken cancellationToken)
        {
            var fortResult = await _db.QueryAsync(new QueryRequest(_settings.TableName)
            {
                KeyConditionExpression = $"{nameof(BaseEntity.EntityId)} = :id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    {":id", new AttributeValue(request.Id)}
                },
                IndexName = BaseEntity.EntityIdIndex
            });
            var singleOrDefault = fortResult.Items.SingleOrDefault();
            if (singleOrDefault == null) throw new Exception("Festival not found");

            var fort = new Fort(singleOrDefault);
            //await fort.DeleteAsync(_db, _settings.TableName);
        }
    }
}