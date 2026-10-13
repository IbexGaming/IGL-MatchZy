using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MatchZy
{
    public partial class MatchZy
    {
        // An event waiting to be sent. The target URL and header are captured when the event is
        // queued so that events still reach the endpoint that was configured when they happened.
        private class QueuedEvent
        {
            public required string EventName { get; init; }
            public required string JsonString { get; init; }
            public required string URL { get; init; }
            public required string HeaderKey { get; init; }
            public required string HeaderValue { get; init; }
            public required long MatchId { get; init; }
            public required int MapNumber { get; init; }
            public TaskCompletionSource Sent { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // All remote log events go through this channel and are sent one at a time by
        // ProcessEventQueueAsync, so a retrying event holds back later events and order is kept.
        private readonly Channel<QueuedEvent> eventQueue = Channel.CreateUnbounded<QueuedEvent>(
            new UnboundedChannelOptions { SingleReader = true }
        );

        // Queues the event for sending. The returned task completes once the event was sent or
        // finally failed.
        public Task SendEventAsync(MatchZyEvent @event)
        {
            try
            {
                if (string.IsNullOrEmpty(matchConfig.RemoteLogURL))
                    return Task.CompletedTask;

                var queuedEvent = new QueuedEvent
                {
                    EventName = @event.EventName,
                    JsonString = JsonSerializer.Serialize(@event, @event.GetType()),
                    URL = matchConfig.RemoteLogURL,
                    HeaderKey = matchConfig.RemoteLogHeaderKey,
                    HeaderValue = matchConfig.RemoteLogHeaderValue,
                    MatchId = liveMatchId,
                    MapNumber = matchConfig.CurrentMapNumber,
                };

                if (!eventQueue.Writer.TryWrite(queuedEvent))
                {
                    Log(
                        $"[SendEventAsync ERROR] Event queue is closed, dropping {@event.EventName} for matchId: {liveMatchId}"
                    );
                    return Task.CompletedTask;
                }

                return queuedEvent.Sent.Task;
            }
            catch (Exception e)
            {
                Log($"[SendEventAsync FATAL] An error occurred: {e.Message}");
                return Task.CompletedTask;
            }
        }

        private async Task ProcessEventQueueAsync()
        {
            await foreach (QueuedEvent queuedEvent in eventQueue.Reader.ReadAllAsync())
            {
                try
                {
                    await PostEventAsync(queuedEvent);
                }
                catch (Exception e)
                {
                    Log($"[SendEventAsync FATAL] An error occurred: {e.Message}");
                }
                finally
                {
                    queuedEvent.Sent.TrySetResult();
                }
            }
        }

        private async Task PostEventAsync(QueuedEvent queuedEvent)
        {
            Log(
                $"[SendEventAsync] Sending Event: {queuedEvent.EventName} for matchId: {queuedEvent.MatchId} mapNumber: {queuedEvent.MapNumber} on {queuedEvent.URL}"
            );

            using var httpClient = new HttpClient();

            Log($"[SendEventAsync] SENDING DATA: {queuedEvent.JsonString}");

            if (
                !string.IsNullOrEmpty(queuedEvent.HeaderKey)
                && !string.IsNullOrEmpty(queuedEvent.HeaderValue)
            )
            {
                httpClient.DefaultRequestHeaders.Add(
                    queuedEvent.HeaderKey,
                    queuedEvent.HeaderValue
                );
            }

            using var httpResponseMessage = await PostWithRetriesAsync(
                httpClient,
                queuedEvent.URL,
                () => new StringContent(queuedEvent.JsonString, Encoding.UTF8, "application/json"),
                "[SendEventAsync]"
            );

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                Log(
                    $"[SendEventAsync] Sending {queuedEvent.EventName} for matchId: {queuedEvent.MatchId} mapNumber: {queuedEvent.MapNumber} successful with status code: {httpResponseMessage.StatusCode}"
                );
            }
            else
            {
                Log(
                    $"[SendEventAsync] Sending {queuedEvent.EventName} for matchId: {queuedEvent.MatchId} mapNumber: {queuedEvent.MapNumber} failed with status code: {httpResponseMessage.StatusCode}, ResponseContent: {await httpResponseMessage.Content.ReadAsStringAsync()}"
                );
            }
        }
    }
}
