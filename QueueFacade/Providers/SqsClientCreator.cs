// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Providers
{
    using Amazon;
    using Amazon.Runtime;
    using Amazon.SQS;

    /// <summary>
    /// Creates <see cref="IAmazonSQS"/> clients. Overridable in tests.
    /// Uses the default AWS credential chain unless explicit keys are set on the config.
    /// </summary>
    internal class SqsClientCreator
    {
        internal virtual IAmazonSQS CreateClient(SqsQueueProviderConfig config)
        {
            var sqsConfig = new AmazonSQSConfig();
            if (!string.IsNullOrWhiteSpace(config.ServiceUrl))
            {
                sqsConfig.ServiceURL = config.ServiceUrl;
                // LocalStack / custom endpoints often need path-style + explicit region
                if (!string.IsNullOrWhiteSpace(config.Region))
                {
                    sqsConfig.AuthenticationRegion = config.Region;
                }
            }
            else
            {
                sqsConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);
            }

            if (!string.IsNullOrEmpty(config.AccessKeyId) && !string.IsNullOrEmpty(config.SecretAccessKey))
            {
                var creds = new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);
                return new AmazonSQSClient(creds, sqsConfig);
            }

            return new AmazonSQSClient(sqsConfig);
        }
    }
}
