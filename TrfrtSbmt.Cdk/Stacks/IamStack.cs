﻿using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;

namespace TrfrtSbmt.Cdk.Stacks;

public class IamStack : Stack
{
    public IamStack(Construct scope, string id, IamStackProps props) : base(scope, id, props)
    {
        var table = Table.FromTableArn(this, "table", props.Table.TableArn);
        var policy = new Policy(this, "smbt-policy", new PolicyProps
        {
            PolicyName = "sbmt-policy",
            Roles = new Role[] { props.Role },
            Statements = new PolicyStatement[]
            {
                new PolicyStatement(new PolicyStatementProps
                {
                    Effect = Effect.ALLOW,
                    Actions = new string[] { "dynamodb:*" },
                    Resources = new string[]
                    {
                        table.TableArn,
                        table.TableArn + "/index/*",
                        table.TableStreamArn
                        //props.TestTable.TableArn,
                        //props.TestTable.TableArn + "/index/*"
                    }
                }),
                new PolicyStatement(new PolicyStatementProps
                {
                    Effect = Effect.ALLOW,
                    Actions = new string[] { "ses:SendEmail", "ses:VerifyEmailIdentity" },
                    Resources = new string[] { "*" }
                }),
                new PolicyStatement(new PolicyStatementProps
                {
                    Effect = Effect.ALLOW,
                    Actions = new string[] { "s3:*" },
                    Resources = new string[]
                    {
                        props.Bucket.BucketArn,
                        props.Bucket.BucketArn + "/*"
                    }
                })
            }
        });


        //var votePolicy = new Policy(this, "vote-policy", new PolicyProps
        //{
        //    PolicyName = "vote-policy",
        //    Roles = new Role[] { props.VoteRole },
        //    Statements = new PolicyStatement[]
        //    {
        //        new PolicyStatement(new PolicyStatementProps
        //        {
        //            Effect = Effect.ALLOW,
        //            Actions = new string[] { "dynamodb:*" },
        //            Resources = new string[]
        //            {
        //                props.Table.TableArn,
        //                props.Table.TableArn + "/index/*",
        //                props.Table.TableStreamArn,
        //                //props.TestTable.TableArn,
        //                //props.TestTable.TableArn + "/index/*",
        //                //props.TestTable.TableStreamArn,
        //            }
        //        })
        //    }
        //});
    }

    public class IamStackProps : StackProps
    {
        public Role VoteRole { get; set; }
        public Role Role { get; set; }
        public Table Table { get; set; }
        //public Table TestTable { get; set; }
        public Bucket Bucket { get; set; }
        public string EnvironmentName { get; init; } = "Development";
        public string EnvironmentPrefix { get; init; } = "Development-";
    }
}
