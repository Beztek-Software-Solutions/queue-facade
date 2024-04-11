// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Queue.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;
    using Queue;
    using Queue.Providers;

    [TestFixture]
    public class QueueClientTest
    {
        private readonly ILogger logger = new LoggerFactory().CreateLogger<QueueClientTest>();
        private readonly string message = "test message";
        private readonly string activityId = "testActivityId";
        private LocalMemoryQueueProvider queueProvider;
        private QueueClient queueClient;

        [SetUp]
        public void TestInitialize()
        {
            this.queueProvider = new LocalMemoryQueueProvider(logger);
            this.queueClient = new QueueClient("TestQueueClient", queueProvider, logger);
        }

        [Test]
        public void TestLocalMemoryWithDaemonConstructor()
        {
            _ = new LocalMemoryQueueProvider(null, true);
        }

        [Test]
        public void EnqueTest()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                _ = queueClient.Enqueue(message, isHighPriorityQueue, activityId);

                // Assert
                Assert.AreEqual(1, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueueTest_LargeCompliantSize()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                string s = "hello world";
                int l = 0;
                while (true)
                {
                    l = MessageUtils.GetMessageSize(s + s);
                    if (l >= queueProvider.MaxMessageSize)
                        break;
                    s = s + s;
                }

                bool response = queueClient.Enqueue(s, isHighPriorityQueue, "myactivityId").Result;

                // Assert
                Assert.IsTrue(response);
                Assert.AreEqual(1, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueueTest_TooLarge()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                string s = "hello world";
                int l = 0;
                while (true)
                {
                    s = s + s;
                    l = MessageUtils.GetMessageSize(s);
                    if (MessageUtils.GetMessageSize(s) > queueProvider.MaxMessageSize)
                        break;
                }

                Assert.Throws<AggregateException>(() => _ = queueClient.Enqueue(s, isHighPriorityQueue, "myactivityId").Result);
            }
        }

        [Test]
        public void EnqueueTest_Fail()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                queueProvider.enqueueFailFlag = true;
                string s = "hello world";
                int l = 0;
                while (true)
                {
                    l = MessageUtils.GetMessageSize(s + s);
                    if (l >= queueProvider.MaxMessageSize)
                        break;
                    s = s + s;
                }

                bool response = queueClient.Enqueue(s, isHighPriorityQueue, "myactivityId").Result;

                // Assert
                Assert.IsFalse(response);
                Assert.AreEqual(0, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueueLargeList_Empty_List()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                List<string> emptyList = new List<string>();

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(emptyList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(0, unSentMessages.Count);
                Assert.AreEqual(0, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueueLargeList_One_Element_Success()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                List<string> oneElementList = new List<string>() { "String1" };

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(oneElementList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(0, unSentMessages.Count);
                Assert.AreEqual(1, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueueLargeList_Success()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                queueProvider.MaxMessageSize = 2000;
                List<string> largeList = new List<string>();
                for (int i = 0; i < 1000; i++)
                {
                    largeList.Add("String");
                }

                // Act
                IList<bool> result = queueClient.Enqueue<string>(largeList, isHighPriorityQueue, activityId).Result;

                // Assert
                for (int i = 0; i < 1000; i++)
                {
                    Assert.IsTrue(result[i]);
                }
            }
        }

        [Test]
        public void EnqueueLargeList_Failure()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                queueProvider.MaxMessageSize = 100;
                List<string> largeList = new List<string>();
                largeList.Add("SingleBigStringOverLimitShouldNotBeSent");
                for (int i = 0; i < 1000; i++)
                {
                    largeList.Add("String");
                }

                // Act
                IList<bool> result = queueClient.Enqueue<string>(largeList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.IsFalse(result[0]);
                for (int i = 1; i < 1001; i++)
                {
                    Assert.IsTrue(result[i]);
                }
            }
        }

        [Test]
        public void EnqueueBatchedLargeList_Success()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                queueProvider.MaxMessageSize = 2000;
                List<string> largeList = new List<string>();
                for (int i = 0; i < 1000; i++)
                {
                    largeList.Add("String");
                }

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(largeList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(0, unSentMessages.Count);
            }
        }

        [Test]
        public void EnqueueLargeList_One_Big_Element_Failure()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                this.queueProvider.MaxMessageSize = 25;
                List<string> oneElementList = new List<string>() { "SingleBigStringOverLimitShouldNotBeSent" };

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(oneElementList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(1, unSentMessages.Count);
            }
        }

        [Test]
        public void EnqueueLargeList_MoreThanOne_Big_Element_Failure()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                this.queueProvider.MaxMessageSize = 25;
                List<string> twoElementList = new List<string>() { "FirstBigStringOverLimitShouldNotBeSent", "SecondBigStringOverLimitShouldNotBeSent" };

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(twoElementList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(2, unSentMessages.Count);
            }
        }

        [Test]
        public void EnqueueLargeList_Two_Elements_Success_Should_Call_Enqueue_Twice()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                this.queueProvider.MaxMessageSize = 125;
                string firstBigStringJustUnderLimit = "First";
                string secondString = "Second";
                List<string> firstSubList = new List<string> { firstBigStringJustUnderLimit };
                List<string> secondSubList = new List<string> { secondString };
                int queueMessageLimit = MessageUtils.GetMessageSize(firstSubList, activityId) + 1;

                List<string> twoElementsList = new List<string>();
                twoElementsList.AddRange(firstSubList);
                twoElementsList.AddRange(secondSubList);

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(twoElementsList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(0, unSentMessages.Count);
                Assert.AreEqual(2, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueueLargeList_Two_Small_Elements_Success_Should_Call_Enqueue_Once()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                // Arrange
                string firstBigStringJustUnderLimit = "First";
                string secondString = "Second";
                List<string> firstSubList = new List<string> { firstBigStringJustUnderLimit };
                List<string> secondSubList = new List<string> { secondString };
                int queueMessageLimit = MessageUtils.GetMessageSize(firstSubList, activityId) + 1;

                List<string> twoElementsList = new List<string>();
                twoElementsList.AddRange(firstSubList);
                twoElementsList.AddRange(secondSubList);

                // Act
                var unSentMessages = queueClient.EnqueueBatchedMessages(twoElementsList, isHighPriorityQueue, activityId).Result;

                // Assert
                Assert.AreEqual(0, unSentMessages.Count);
                Assert.AreEqual(1, this.queueProvider.GetNumMessages(isHighPriorityQueue));
            }
        }

        [Test]
        public void EnqueUnprocessedTest()
        {
            bool results = queueClient.EnqueueUnprocessedMessages<string>(message, activityId).Result;

            // Assert
            Assert.IsTrue(results);
            Assert.AreEqual(1, this.queueProvider.GetNumUnprocessedMessages().Result);
        }

        [Test]
        public void EnqueUnprocessedTest_TooLarge()
        {
            string s = "hello world";
            int l = 0;
            while (true)
            {
                s = s + s;
                l = MessageUtils.GetMessageSize(s);
                if (MessageUtils.GetMessageSize(s) > queueProvider.MaxMessageSize)
                    break;
            }

            Assert.Throws<AggregateException>(() => _ = queueClient.EnqueueUnprocessedMessages(s, "myactivityId").Result);
        }

        [Test]
        public void EnqueueUnprocessedTest_Fail()
        {
            queueProvider.enqueueFailFlag = true;
            string s = "hello world";

            bool response = queueClient.EnqueueUnprocessedMessages(s, "myactivityId").Result;

            // Assert
            Assert.IsFalse(response);
            Assert.AreEqual(0, this.queueProvider.GetNumUnprocessedMessages().Result);
        }

        [Test]
        public void DequeueTest()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                int numMessages = 5;
                int pollingIntervalMillis = 10;
                int maxAsynchronousProcesses = 2;
                int maxMessageRate = 1000;
                int batchSize = 1;
                for (int i = 0; i < numMessages; i++)
                {
                    _ = queueClient.Enqueue($"{i}", isHighPriorityQueue).Result;
                }

                Assert.AreEqual(numMessages, this.queueProvider.GetNumMessages(isHighPriorityQueue));

                TestMessageProcessor messageProcessor = new TestMessageProcessor();
                IQueueProcessorHandler handler = IQueueProcessorHandler.Default().AddProcessor(typeof(string), messageProcessor);
                CancellationTokenSource cts = new CancellationTokenSource();

                _ = this.queueClient.DequeueAndProcess(maxMessageRate, maxAsynchronousProcesses, messageProcessor, cts.Token, batchSize, pollingIntervalMillis);

                Task.Run(() => {
                    Thread.Sleep(((1000 * numMessages + maxMessageRate - 1) / maxMessageRate) * pollingIntervalMillis + 1 + 2000);
                    Assert.IsTrue(this.queueClient.StopDequeuing().Result);

                    Assert.AreEqual(numMessages, messageProcessor.GetProcessCount());
                    Assert.AreEqual(0, this.queueProvider.GetNumMessages(isHighPriorityQueue));
                }).Wait();
            }
        }

        [Test]
        public void DequeueBatchTest()
        {
            foreach (bool isHighPriorityQueue in new bool[] { true, false })
            {
                int numMessages = 5;
                int pollingIntervalMillis = 10;
                int maxAsynchronousProcesses = 2;
                int maxMessageRate = 1000;
                int batchSize = 2;
                for (int i = 0; i < numMessages; i++)
                {
                    _ = queueClient.Enqueue($"{i}", isHighPriorityQueue).Result;
                }

                Assert.AreEqual(numMessages, this.queueProvider.GetNumMessages(isHighPriorityQueue));

                TestMessageProcessor messageProcessor = new TestMessageProcessor();
                IQueueProcessorHandler handler = IQueueProcessorHandler.Default().AddProcessor(typeof(string), messageProcessor);
                CancellationTokenSource cts = new CancellationTokenSource();

                _ = this.queueClient.DequeueAndProcess(maxMessageRate, maxAsynchronousProcesses, handler, cts.Token, batchSize, pollingIntervalMillis);

                Task.Run(() => {
                    Thread.Sleep(((1000 * numMessages + maxMessageRate - 1) / maxMessageRate) * pollingIntervalMillis + 1 + 2000);
                    Assert.IsTrue(this.queueClient.StopDequeuing().Result);

                    Assert.AreEqual((numMessages + batchSize - 1) / batchSize, messageProcessor.GetProcessListCount());
                    Assert.AreEqual(numMessages, messageProcessor.GetProcessCount());
                    Assert.AreEqual(0, this.queueProvider.GetNumMessages(isHighPriorityQueue));
                }).Wait();
            }
        }
    }
}