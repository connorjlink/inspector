using inspector;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Documents;

// AI-assisted
public class MqttScheduler
{
    private class ScheduledMessage
    {
        public System.Timers.Timer _timer;
        public bool _paused;

        public ScheduledMessage(System.Timers.Timer timer)
        {
            _timer = timer;
            _paused = false;
        }
    }

    private ViewModel _viewmodel;
    private ConcurrentDictionary<string, ScheduledMessage> _scheduledMessages = new();

    //private ConcurrentDictionary<string, System.Timers.Timer> ScheduledMessages
    //{
    //    get
    //    {
    //        return _scheduledMessages;
    //    }
    //}

    public MqttScheduler(ViewModel viewmodel)
    {
        _viewmodel = viewmodel;
    }

    public async Task PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos, bool retain)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
                .WithPayload(payload)
                    .WithQualityOfServiceLevel(qos)
                        .WithRetainFlag(retain)
                            .Build();

        await ViewModel._mqttClient.PublishAsync(message);
    }

    public void ScheduleMessage(string topic, string payload, MqttQualityOfServiceLevel qos, bool retain, int intervalInMilliseconds)
    {
        if (_scheduledMessages.ContainsKey(topic))
        {
            throw new InvalidOperationException($"A schedule for '{topic}' already exists");
        }

        var timer = new System.Timers.Timer(intervalInMilliseconds)
        {
            AutoReset = true,
            Enabled = true
        };

        timer.Elapsed += async (sender, e) => await PublishAsync(topic, payload, qos, retain);

        // NOTE: the Messaage constructor defaults to *not paused*
        _scheduledMessages[topic] = new ScheduledMessage(timer);

        _viewmodel.WriteConsole($"Started transmitting {topic} every {intervalInMilliseconds} ms", ViewModel.INFO);
    }
    
    public bool IsMessageScheduled(string topic)
    {
        return _scheduledMessages.ContainsKey(topic);
    }

    public bool IsMessagePaused(string topic)
    {
        if (_scheduledMessages.TryGetValue(topic, out var message))
        {
            if (message._paused)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryPauseMessage(string topic)
    {
        if (_scheduledMessages.TryGetValue(topic, out var message))
        {
            _viewmodel.WriteConsole($"Paused {topic}", ViewModel.INFO);
            message._timer.Stop();
            message._paused = true;
            return true;
        }

        return false;
    }

    public bool TryResumeMessage(string topic)
    {
        if (_scheduledMessages.TryGetValue(topic, out var message))
        {
            _viewmodel.WriteConsole($"Resumed {topic}", ViewModel.INFO);
            message._timer.Start();
            message._paused = false;
            return true;
        }

        return false;
    }

    public bool TryRemoveMessageSchedule(string topic)
    {
        if (_scheduledMessages.TryRemove(topic, out var message))
        {
            _viewmodel.WriteConsole($"Stopped transmitting {topic}", ViewModel.INFO);
            message._timer.Stop();
            message._timer.Dispose();
            return true;
        }

        return false;
    }
}