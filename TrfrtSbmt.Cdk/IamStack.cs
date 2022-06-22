﻿using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;

namespace TrfrtSbmt.Cdk;

public class IamStack : Stack
{
    public IamStack(Construct scope, string id, IamStackProps props) : base(scope, id, props) 
    {
        var policy = new Policy(this, "table-access-policy", new PolicyProps 
        {
            PolicyName = "table-access-policy",
            Roles = new Role[] { props.Role },
            Statements = new PolicyStatement[]
            {
                new PolicyStatement(new PolicyStatementProps
                {
                    Effect = Effect.ALLOW,
                    Actions = new string[] { "dynamodb:*" },
                    Resources = new string[]
                    {
                        props.Table.TableArn,
                        props.Table.TableArn + "/index/*",
                        props.TestTable.TableArn,
                        props.TestTable.TableArn + "/index/*"
                    }
                })
            }
        });        
    }

    public class IamStackProps : StackProps
    {
        public Amazon.CDK.Environment Env { get; set; }
        public Role Role { get; set; }
        public Table Table { get; set; }
        public Table TestTable { get; set; }
    }
}
