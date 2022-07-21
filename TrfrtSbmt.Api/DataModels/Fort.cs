﻿using Amazon.DynamoDBv2.Model;

namespace TrfrtSbmt.Api.DataModels;

public class Fort : BaseEntity
{
    protected override string SortKeyPrefix => $"{nameof(Fort)}-";
        
    public Fort(Dictionary<string, AttributeValue> dictionary) : base(dictionary) { }
    public Fort(string festivalId, string name, string description, string createdBy) : base(festivalId, name, name, createdBy) 
    {
        _attributes[nameof(Description)] = new AttributeValue(description);
    }

    public string Description => GetDescription();//_attributes[nameof(Description)].S;

    private string GetDescription()
    {
        if (_attributes.TryGetValue(nameof(Description), out var description))
            return description.S;
        return string.Empty;
    }
}
