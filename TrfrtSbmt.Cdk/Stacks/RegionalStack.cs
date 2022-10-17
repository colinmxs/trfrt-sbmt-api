﻿namespace TrfrtSbmt.Cdk.Stacks;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.S3;
using static Amazon.CDK.AWS.Route53.CfnHealthCheck;

public class RegionalStack : Stack
{
    public class RegionalStackProps : StackProps
    {
        public string EnvironmentName { get; init; } = "Development";
        public string EnvironmentSuffix { get; init; } = "Development-";
        //public string Name { get; init; } = "TreefortSubmitApi";
        public string Region { get; init; } = "us-east-1";
        public string PrimaryRegion { get; init; } = "us-west-2";
        public string RegionalCertId { get; init; } = string.Empty;
        public string GlobalCertId { get; init; } = string.Empty;
    }

    public RegionalStack(Construct scope, string id, RegionalStackProps props) : base(scope, id, props)
    {
        Amazon.CDK.Tags.Of(this).Add("Billing", "Treefort");

        var s3Bucket = Bucket.FromBucketName(this, "SubmissionsBucket", props.EnvironmentName == "Production" ? "sbmt-api-1" : "development-sbmt-api-1");
        if (s3Bucket == null) throw new Exception("Bucket not found");
        if (s3Bucket.BucketArn == null) throw new Exception("Bucket ARN not found");


        var accountId = (string)scope.Node.TryGetContext("accountid");
        var domain = (string)scope.Node.TryGetContext("domain");
        var subdomain = (string)scope.Node.TryGetContext("subdomain");
        ITable table;
        if(props.Region == props.PrimaryRegion)
        {
            table = new SubmissionsTable(this, "SubmissionsDynamoTable", new SubmissionsTableProps
            {
                EnvironmentName = props.EnvironmentName,
                RemovalPolicy = RemovalPolicy.RETAIN,
                TableName = $"Submissions{props.EnvironmentSuffix}",
                ReplicationRegion = "us-west-1"
            }).Table;
            Amazon.CDK.Tags.Of(table).Add("Name", $"Submissions{props.EnvironmentSuffix}");
            Amazon.CDK.Tags.Of(table).Add("Last Updated", DateTimeOffset.UtcNow.ToString());
        }
        else
        {
            table = Table.FromTableArn(this, "SubmissionsDynamoTable", $"arn:aws:dynamodb:{props.PrimaryRegion}:{accountId}:table/Submissions{props.EnvironmentSuffix}");
        }

        if (table == null) throw new Exception("Table not found");
        if (table.TableArn == null) throw new Exception("Table ARN not found");
        if (table.TableStreamArn == null) throw new Exception("Table Stream ARN not found");
        
        var lambdaExecutionRole = new Role(this, "SubmissionsApiLambdaExecutionRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            RoleName = $"SubmissionsApiLambdaExecutionRole{props.EnvironmentSuffix}",
            InlinePolicies = new Dictionary<string, PolicyDocument>
            {
                {
                    "cloudwatch-policy",
                    new PolicyDocument(new PolicyDocumentProps
                    {
                        AssignSids = true,
                        Statements = new []
                        {
                            new PolicyStatement(new PolicyStatementProps {
                                Effect = Effect.ALLOW,
                                Actions = new string[] {
                                    "logs:CreateLogStream",
                                    "logs:PutLogEvents",
                                    "logs:CreateLogGroup"
                                },
                                Resources = new string[] {
                                    "arn:aws:logs:*:*:*"
                                }
                            })
                        }
                    })
                },
                {
                    "dynamodb-policy",
                    new PolicyDocument(new PolicyDocumentProps
                    {
                        AssignSids = true,
                        Statements = new []
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
                            })
                        }
                    })
                },
                {
                    "s3-policy",
                    new PolicyDocument(new PolicyDocumentProps
                    {
                        AssignSids = true,
                        Statements = new []
                        {                            
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Effect = Effect.ALLOW,
                                Actions = new string[] { "s3:*" },
                                Resources = new string[]
                                {
                                    s3Bucket.BucketArn,
                                    s3Bucket.BucketArn + "/*"
                                }
                            })
                        }
                    })
                },
                {
                    "ses-policy",
                    new PolicyDocument(new PolicyDocumentProps
                    {
                        AssignSids = true,
                        Statements = new []
                        {
                            new PolicyStatement(new PolicyStatementProps
                            {
                                Effect = Effect.ALLOW,
                                Actions = new string[] { "ses:SendEmail", "ses:VerifyEmailIdentity" },
                                Resources = new string[] { "*" }
                            })
                        }
                    })
                }
            }
        });
        Amazon.CDK.Tags.Of(lambdaExecutionRole).Add("Name", $"SubmissionsApiLambdaExecutionRole{props.EnvironmentSuffix}");
        Amazon.CDK.Tags.Of(lambdaExecutionRole).Add("Last Updated", DateTimeOffset.UtcNow.ToString());

        var lambdaFunction = new Function(this, "SubmissionsApiLambdaFunction", new FunctionProps
        {
            Code = new AssetCode($"{Utilities.GetDirectory("TrfrtSbmt.Api")}"),
            Handler = "TrfrtSbmt.Api",
            Runtime = Runtime.DOTNET_6,
            Timeout = Duration.Seconds(10),
            FunctionName = $"SubmissionsApiLambdaFunction{props.EnvironmentSuffix}",
            MemorySize = 2048,
            RetryAttempts = 1,
            Role = lambdaExecutionRole,
            Environment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = props.EnvironmentName
            }
        });
        Amazon.CDK.Tags.Of(lambdaFunction).Add("Name", $"SubmissionsApiLambdaFunction{props.EnvironmentSuffix}");
        Amazon.CDK.Tags.Of(lambdaFunction).Add("Last Updated", DateTimeOffset.UtcNow.ToString());

        var restApi = new LambdaRestApi(this, "SubmissionsRestApi", new LambdaRestApiProps
        {
            DeployOptions = new StageOptions
            {
                StageName = "v1"
            },
            Handler = lambdaFunction,
            Proxy = true,
            Deploy = true,
            DefaultCorsPreflightOptions = new CorsOptions
            {
                AllowOrigins = Cors.ALL_ORIGINS,
                AllowMethods = Cors.ALL_METHODS,
                AllowHeaders = Cors.DEFAULT_HEADERS
            },
            RestApiName = $"SubmissionsRestApi{props.EnvironmentSuffix}",
            EndpointTypes = new EndpointType[]
            {
                EndpointType.REGIONAL
            },
            DomainName = new DomainNameOptions
            {
                Certificate = Certificate.FromCertificateArn(this, "cert", $"arn:aws:acm:{props.Region}:{accountId}:certificate/{props.RegionalCertId}"),
                DomainName = $"{props.EnvironmentSuffix.ToLower()}{subdomain}.{domain}",
                EndpointType = EndpointType.REGIONAL,
                SecurityPolicy = SecurityPolicy.TLS_1_2
            }
        });
        Amazon.CDK.Tags.Of(restApi).Add("Name", $"{props.EnvironmentSuffix.ToLower()}{subdomain}.{domain}");
        Amazon.CDK.Tags.Of(restApi).Add("Last Updated", DateTimeOffset.UtcNow.ToString());

        var healthCheck = new CfnHealthCheck(this, $"{props.Region}-HealthCheck", new CfnHealthCheckProps
        {
            HealthCheckConfig = new HealthCheckConfigProperty
            {
                Type = "HTTPS",
                FullyQualifiedDomainName = $"{restApi.RestApiId}.execute-api.{props.Region}.amazonaws.com",
                Port = 443,
                ResourcePath = $"/{restApi.DeploymentStage.StageName}/health-check"
            }
        });

        var route53 = new ARecord(this, $"SubmissionsARecord", new ARecordProps
        {
            Zone = HostedZone.FromLookup(this, $"{props.Region}-HostedZone", new HostedZoneProviderProps
            {
                DomainName = domain
            }),
            Target = RecordTarget.FromAlias(new ApiGateway(restApi)),
            RecordName = $"{props.EnvironmentSuffix.ToLower()}{subdomain}.{domain}"
        });
        Amazon.CDK.Tags.Of(route53).Add("Name", domain);
        Amazon.CDK.Tags.Of(route53).Add("Last Updated", DateTimeOffset.UtcNow.ToString());

        if (route53.Node.DefaultChild is not CfnRecordSet recordSet)
        {
            throw new Exception("Alias record has no record set node. Cannot attach health check.");
        }

        recordSet.Region = props.Region;
        recordSet.HealthCheckId = healthCheck.AttrHealthCheckId;
        recordSet.SetIdentifier = $"{props.Region}-Endpoint";



        //lambdaExecutionRole = new Role(this, "ApiLambdaExecutionRole", new RoleProps
        //{
        //    AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
        //    RoleName = $"{props.EnvironmentSuffix}{props.Name}LambdaExecutionRole",
        //    InlinePolicies = new Dictionary<string, PolicyDocument>
        //    {
        //        {
        //            "cloudwatch-policy",
        //            new PolicyDocument(
        //                new PolicyDocumentProps {
        //                    AssignSids = true,
        //                    Statements = new [] {
        //                        new PolicyStatement(new PolicyStatementProps {
        //                            Effect = Effect.ALLOW,
        //                            Actions = new string[] {
        //                                "logs:CreateLogStream",
        //                                "logs:PutLogEvents",
        //                                "logs:CreateLogGroup"
        //                            },
        //                            Resources = new string[] {
        //                                "arn:aws:logs:*:*:*"
        //                            }
        //                        })
        //                    }
        //                })
        //        }
        //    }
        //});
        //Amazon.CDK.Tags.Of(lambdaExecutionRole).Add("Name", $"{props.EnvironmentSuffix}{props.Name}LambdaExecutionRole");
        //Amazon.CDK.Tags.Of(lambdaExecutionRole).Add("Last Updated", DateTimeOffset.UtcNow.ToString());

        //var targetFunction = new Function(this, "VoteStream.Function", new FunctionProps
        //{
        //    Runtime = Runtime.DOTNET_6,
        //    Code = new AssetCode($"{Utilities.GetDirectory("TrfrtSbmt.VoteStreamProcessor")}"),
        //    Handler = "TrfrtSbmt.VoteStreamProcessor",
        //    Timeout = Duration.Seconds(10),
        //    FunctionName = $"{props.EnvironmentSuffix}{props.Name}LambdaFunction",
        //    MemorySize = 2048,
        //    RetryAttempts = 1,
        //    Role = lambdaExecutionRole,
        //    Environment = new Dictionary<string, string>
        //    {
        //        ["ASPNETCORE_ENVIRONMENT"] = props.EnvironmentName
        //    }
        //});
        //Amazon.CDK.Tags.Of(targetFunction).Add("Name", $"{props.EnvironmentSuffix}{props.Name}LambdaFunction");
        //Amazon.CDK.Tags.Of(targetFunction).Add("Last Updated", DateTimeOffset.UtcNow.ToString());
    }
}
