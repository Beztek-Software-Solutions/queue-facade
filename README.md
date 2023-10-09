# Queue Facade Library

This library is intended for inter-service communication by use of Queues. 

# Overview

It is intended to be cloud portable and take advantage of the native managed services in each cloud, such as Azure Queue Storage and AWS Simple Queue Service.

It is a reusable and configurable queue library that can ensure that just one consumer among multiple competing consumers processes each message. 
This library should be available for any micro-service for this use case.

## Notes

Every queue within an account must have a unique name. The queue name must be a valid DNS name, and cannot be changed once created. Queue names must confirm to the following rules:

   - A queue name must start with a letter or number, and can only contain letters, numbers, and the dash (-) character.

   - The first and last letters in the queue name must be alphanumeric. The dash (-) character cannot be the first or last character. Consecutive dash characters are not permitted in the queue name.

   - All letters in a queue name must be lowercase.

   - A queue name must be from 3 through 63 characters long.

## Steps to use Queue Facade

1. Find Azure storage connection string and queue names, or create a new queue by providing a new queue name
2. Implement callback interface IMessageProcessor such as class ProcessMessage
3. Use QueueClientFactory to create Queue client by passing connection string and at lease one queue name
       client=QueueClientFactory.GeteQueueClient(…)
4. Use client.Enqueue(…) to send generic message to queue, or client.EnqueueBatchedMessages(...) to send list of messages in batch mode.
       Example1:  bool result = await client.Enqueue<string>(stringMessage, true, activityId);
       Example2:  IList<bool> results = await client.Enqueue<string>(stringList, true, activityId);
       Example3:  List<string> unsentMessages = await client.EnqueueBatchedMessages<string>(stringList, true, activityId);

       Notice that example3 batch input stringList in chunks, each chunk includes a sub-list of input.
       This not only result in less messages in queue than example2, but also allow consumer(etc. event scheduler) to handle batched messages more efficiently. 
    
5. Create an instance of ProcessMessage, ProcessMessage callback = new ProcessMessage()
6. Use client.DequeueAndProcess(… callback) to retrieve messages from queue
7. The callback instance should have the messages 

### Critical Details

1. Application handling messages implements `IMessageProcessor`, if MessageProcessor implementation throws an exception which is Not `System.ApplicationException` then the Queue will again make the message visible to the message processor until message processor succeeds.
2. If MessageProcessor needs to handle some application exceptions such as ValidationException, it should catch and throw `System.ApplicationException` in order to avoid getting same message again and again.
3. In the MessageProcessor, we need to use the UnwrappedMessage class and not string, it will throw an exception.
